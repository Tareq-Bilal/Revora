using System.Security.Claims;
using MongoDB.Driver;
using MongoDB.Entities;
using MassTransit;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SearchService.Consumers;
using Polly;
using Polly.Extensions.Http;
using SearchService.Data;
using SearchService.Entities;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services
    .AddOptions<IdentityServiceOptions>()
    .Bind(builder.Configuration.GetSection(IdentityServiceOptions.SectionName))
    .Validate(
        options =>
            Uri.TryCreate(
                options.Authority,
                UriKind.Absolute,
                out _),
        "IdentityService:Authority must be an absolute URL.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Audience) &&
            !string.IsNullOrWhiteSpace(options.ClientId) &&
            !string.IsNullOrWhiteSpace(options.ClientSecret) &&
            !string.IsNullOrWhiteSpace(options.Scope),
        "IdentityService audience and client-credentials settings are required.")
    .ValidateOnStart();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityService:Authority"]
            ?? "http://localhost:5001";
        options.Audience = builder.Configuration["IdentityService:Audience"]
            ?? RevoraAuth.SearchApiAudience;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "role",
        };
    });
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        RevoraAuth.SearchReadPolicy,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(context.User, RevoraAuth.UserApiScope)));
builder.Services.AddScoped<ISearchIndexService, SearchIndexService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(
    "identity-token",
    client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["IdentityService:Authority"]
            ?? "http://localhost:5001");
    });
builder.Services.AddSingleton<ClientCredentialsTokenService>();
builder.Services.AddTransient<ClientCredentialsTokenHandler>();
builder.Services
    .AddHttpClient<AuctionSvcHttpClient>()
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>()
    .AddPolicyHandler(GetPolicy());
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AuctionCreatedConsumer>();
    x.AddConsumer<AuctionUpdatedConsumer>();
    x.AddConsumer<AuctionDeletedConsumer>();
    x.AddConsumer<AuctionFinishedConsumer>();

    x.SetEndpointNameFormatter(
        new KebabCaseEndpointNameFormatter(prefix: "search", includeNamespace: false));

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ReceiveEndpoint("search-auction-created", e =>
        {
            e.UseMessageRetry(r => r.Interval(5,5));

            e.ConfigureConsumer<AuctionCreatedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization(); 

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await DbInitializer.InitializeAsync(app);
        Console.WriteLine("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while initializing the database: {ex.Message}");
    }
});

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetPolicy() =>
    HttpPolicyExtensions.HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
    .WaitAndRetryAsync(
    3,
    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
    (outcome, timespan, retryAttempt, context) =>
    {
        Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds} seconds");
    });

static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
    user.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(requiredScope, StringComparer.Ordinal);
