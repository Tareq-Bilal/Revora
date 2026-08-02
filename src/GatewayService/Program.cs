using System.Security.Claims;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityService:Authority"]
            ?? "http://localhost:5001";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "role",
            ValidateAudience = true,
            // The gateway fronts every resource server, so it accepts any
            // audience it routes to. Each service still validates its own.
            ValidAudiences =
            [
                RevoraAuth.AuctionApiAudience,
                RevoraAuth.SearchApiAudience,
            ],
        };
    });
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        RevoraAuth.AuctionWritePolicy,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(context.User, RevoraAuth.UserApiScope)))
    .AddPolicy(
        RevoraAuth.AuctionSyncPolicy,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(context.User, RevoraAuth.InternalSyncScope)));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
    user.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(requiredScope, StringComparer.Ordinal);
