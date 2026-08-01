using System.Net.Http.Headers;

namespace SearchService.Services;

public sealed class ClientCredentialsTokenHandler(
    ClientCredentialsTokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
