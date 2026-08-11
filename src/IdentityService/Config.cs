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
        var postmanSecret = GetRequiredSecret(
            configuration,
            "IdentityClients:Postman:ClientSecret");

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

            // Next.js WebApp BFF using code flow + pkce; the Node server performs the
            // code exchange, so this stays a confidential client and the browser only
            // ever holds the BFF session cookie.
            new Client
            {
                ClientId = "interactive",
                ClientName = "Revora Web App (Next.js BFF)",
                ClientSecrets = { new Secret(interactiveSecret.Sha256()) },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                RedirectUris = { "http://localhost:3001/api/auth/callback/revora" },
                FrontChannelLogoutUri = "http://localhost:3001/api/auth/logout",
                PostLogoutRedirectUris = { "http://localhost:3001" },

                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", RevoraAuth.UserApiScope }
            },

            // resource owner password flow client, for direct API testing (e.g. Postman)
            new Client
            {
                ClientId = "postman",
                ClientName = "Postman Resource Owner Password Client",

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                ClientSecrets = { new Secret(postmanSecret.Sha256()) },

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
