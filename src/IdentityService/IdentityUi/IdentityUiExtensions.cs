using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;

namespace IdentityService.IdentityUi;

internal static class IdentityUiExtensions
{
    internal static async Task<bool> GetSchemeSupportsSignOutAsync(this HttpContext context, string scheme)
    {
        var provider = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(context, scheme);
        return handler is IAuthenticationSignOutHandler;
    }

    internal static bool IsNativeClient(this AuthorizationRequest context) =>
        !context.RedirectUri.StartsWith("https", StringComparison.Ordinal) &&
        !context.RedirectUri.StartsWith("http", StringComparison.Ordinal);

    internal static bool IsRemote(this ConnectionInfo connection)
    {
        var localAddresses = new List<string?> { "127.0.0.1", "::1" };
        if (connection.LocalIpAddress != null)
        {
            localAddresses.Add(connection.LocalIpAddress.ToString());
        }

        return !localAddresses.Contains(connection.RemoteIpAddress?.ToString());
    }
}
