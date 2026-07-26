using System.Diagnostics.Metrics;

namespace IdentityService.IdentityUi;

internal static class IdentityUiTelemetry
{
    private static readonly string ServiceVersion =
        typeof(IdentityUiTelemetry).Assembly.GetName().Version!.ToString();
    private static readonly string ServiceName =
        typeof(IdentityUiTelemetry).Assembly.GetName().Name!;
    private static readonly Meter Meter = new(ServiceName, ServiceVersion);
    private static readonly Counter<long> ConsentCounter =
        Meter.CreateCounter<long>("tokenservice.consent");
    private static readonly Counter<long> GrantsRevokedCounter =
        Meter.CreateCounter<long>("tokenservice.grants_revoked");
    private static readonly Counter<long> UserLoginCounter =
        Meter.CreateCounter<long>("tokenservice.user_login");
    private static readonly Counter<long> UserLogoutCounter =
        Meter.CreateCounter<long>("tokenservice.user_logout");

    internal static class Metrics
    {
        internal static void ConsentGranted(string clientId, IEnumerable<string> scopes, bool remember)
        {
            foreach (var scope in scopes)
            {
                ConsentCounter.Add(
                    1,
                    new("client", clientId),
                    new("scope", scope),
                    new("remember", remember),
                    new("consent", "granted"));
            }
        }

        internal static void ConsentDenied(string clientId, IEnumerable<string> scopes)
        {
            foreach (var scope in scopes)
            {
                ConsentCounter.Add(
                    1,
                    new("client", clientId),
                    new("scope", scope),
                    new("consent", "denied"));
            }
        }

        internal static void GrantsRevoked(string? clientId) =>
            GrantsRevokedCounter.Add(1, tag: new("client", clientId));

        internal static void UserLogin(string? clientId, string idp) =>
            UserLoginCounter.Add(1, new("client", clientId), new("idp", idp));

        internal static void UserLoginFailure(string? clientId, string idp, string error) =>
            UserLoginCounter.Add(1, new("client", clientId), new("idp", idp), new("error", error));

        internal static void UserLogout(string? idp) =>
            UserLogoutCounter.Add(1, tag: new("idp", idp));
    }
}
