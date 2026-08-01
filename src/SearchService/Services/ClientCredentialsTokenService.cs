using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SearchService.Services;

public sealed class ClientCredentialsTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityServiceOptions> identityOptions,
    IMemoryCache cache)
{
    private const string TokenCacheKey = "revora.search-service.access-token";
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<string>(TokenCacheKey, out var cachedToken))
        {
            return cachedToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<string>(TokenCacheKey, out cachedToken))
            {
                return cachedToken!;
            }

            var options = identityOptions.Value;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/connect/token")
            {
                Content = new FormUrlEncodedContent(
                [
                    new("grant_type", "client_credentials"),
                    new("client_id", options.ClientId),
                    new("client_secret", options.ClientSecret),
                    new("scope", options.Scope),
                ]),
            };

            var client = httpClientFactory.CreateClient("identity-token");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"IdentityService rejected the client credentials request with status {(int)response.StatusCode}.");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException(
                    "IdentityService returned an invalid client credentials response.");
            }

            var cacheLifetime = TimeSpan.FromSeconds(token.ExpiresIn) - RefreshSkew;
            if (cacheLifetime <= TimeSpan.Zero)
            {
                cacheLifetime = TimeSpan.FromSeconds(30);
            }

            cache.Set(TokenCacheKey, token.AccessToken, cacheLifetime);
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
