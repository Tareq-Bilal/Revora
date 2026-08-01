using System.Security.Claims;
using AuctionService.Consumers;
using AuctionService.Data;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityService:Authority"]
            ?? "http://localhost:5001";
        options.Audience = builder.Configuration["IdentityService:Audience"]
            ?? RevoraAuth.AuctionApiAudience;
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
builder.Services.AddDbContext<AuctionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add MassTransit and configure RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<AuctionDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(10);
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.AddConsumersFromNamespaceContaining<AuctionCreatedFault>();
    
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(prefix: "auction", includeNamespace: false));
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


try{
    DbInitializer.Initialize(app);
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while seeding the database: {ex.Message}");
}

app.Run();

static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
    user.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(requiredScope, StringComparer.Ordinal);
