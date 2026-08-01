namespace IdentityService.IdentityUi;

public sealed record LoginContextResponse(
    string? ReturnUrl,
    string? Username,
    bool EnableLocalLogin,
    bool AllowRememberLogin);

public sealed record LoginRequest(
    string? Username,
    string? Password,
    bool RememberLogin,
    string? Action,
    string? ReturnUrl);
