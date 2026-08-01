using Contracts;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace IdentityService;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new(RevoraAuth.InternalSyncScope, "Synchronize the auction search index"),
            new(RevoraAuth.UserApiScope, "Read and manage auctions as a signed-in user"),
        };

    public static IEnumerable<ApiResource> ApiResources =>
        new ApiResource[]
        {
            new(RevoraAuth.AuctionApiAudience, "Revora Auction API")
            {
                Scopes =
                {
                    RevoraAuth.InternalSyncScope,
                    RevoraAuth.UserApiScope,
                },
                UserClaims =
                {
                    JwtClaimTypes.Name,
                    JwtClaimTypes.PreferredUserName,
                },
            },
            new(RevoraAuth.SearchApiAudience, "Revora Search API")
            {
                Scopes =
                {
                    RevoraAuth.UserApiScope,
                },
            },
        };

    public static IEnumerable<Client> GetClients(IConfiguration configuration)
    {
        var machineSecret = GetRequiredSecret(
            configuration,
            "IdentityClients:Machine:ClientSecret");
        var interactiveSecret = GetRequiredSecret(
            configuration,
            "IdentityClients:Interactive:ClientSecret");

        return
        new Client[]
        {
            // m2m client credentials flow client
            new Client
            {
                ClientId = "m2m.client",
                ClientName = "Client Credentials Client",

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret(machineSecret.Sha256()) },

                AllowedScopes = { RevoraAuth.InternalSyncScope }
            },

            // interactive client using code flow + pkce
            new Client
            {
                ClientId = "interactive",
                ClientSecrets = { new Secret(interactiveSecret.Sha256()) },

                AllowedGrantTypes = GrantTypes.Code,

                RedirectUris = { "https://localhost:44300/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44300/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44300/signout-callback-oidc" },

                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", RevoraAuth.UserApiScope }
            },
        };
    }

    private static string GetRequiredSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Required IdentityServer secret '{key}' is not configured.");
    }
}
