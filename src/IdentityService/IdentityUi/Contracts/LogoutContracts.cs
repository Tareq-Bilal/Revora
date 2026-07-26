namespace IdentityService.IdentityUi;

public sealed record LogoutContextResponse(
    string? LogoutId,
    bool ShowPrompt,
    bool AutoSubmit);

public sealed record LoggedOutContextResponse(
    string? ClientName,
    string? PostLogoutRedirectUri,
    string? SignOutIframeUrl,
    bool AutomaticRedirectAfterSignOut);
