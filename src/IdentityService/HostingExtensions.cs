using System.Globalization;
using IdentityService.Data;
using IdentityService.IdentityUi;
using IdentityService.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityService;

internal static class HostingExtensions
{
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddSerilog(lc => lc
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                formatProvider: CultureInfo.InvariantCulture)
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(builder.Configuration));
        return builder;
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddOpenApi();
        _ = builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "IdentityService.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
        _ = builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        _ = builder.Services.AddHttpClient("IdentityUi", client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration["IdentityUi:BaseUrl"] ?? "http://127.0.0.1:3000",
                UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(2);
        });

        _ = builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        _ = builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _ = builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.UserInteraction.LoginUrl = "/Account/Login";
                options.UserInteraction.LogoutUrl = "/Account/Logout";
                options.UserInteraction.ConsentUrl = "/Consent";
                options.UserInteraction.ErrorUrl = "/Error";
            })
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryApiResources(Config.ApiResources)
            .AddInMemoryClients(Config.GetClients(builder.Configuration))
            .AddAspNetIdentity<ApplicationUser>()
            .AddProfileService<RevoraProfileService>();

        // add `.PersistKeysTo…()` and `.ProtectKeysWith…()` calls
        // see more at https://docs.duendesoftware.com/general/data-protection
        _ = builder.Services.AddDataProtection()
                   .SetApplicationName("IdentityServer");

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "IdentityService.Cookie";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/identity-ui"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/identity-ui"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
        });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        _ = app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();

        _ = app.MapIdentityUi();

        if (app.Environment.IsDevelopment())
        {
            _ = app.MapOpenApi();
        }

        _ = app.MapGet("/health/identity-ui", async (
            IHttpClientFactory httpClientFactory,
            CancellationToken cancellationToken) =>
        {
            try
            {
                using var response = await httpClientFactory
                    .CreateClient("IdentityUi")
                    .GetAsync("/", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return response.IsSuccessStatusCode
                    ? Results.Ok(new { status = "healthy" })
                    : Results.Problem(
                        "The Identity UI returned an unhealthy response.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    "The Identity UI is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.Problem(
                    "The Identity UI health check timed out.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).ExcludeFromDescription();

        _ = app.MapReverseProxy();

        return app;
    }
}
