namespace IdentityService.IdentityUi;

public sealed record ExternalProviderDto(
    string AuthenticationScheme,
    string DisplayName,
    string ChallengeUrl);

public sealed record LoginContextResponse(
    string? ReturnUrl,
    string? Username,
    bool EnableLocalLogin,
    bool AllowRememberLogin,
    IReadOnlyCollection<ExternalProviderDto> ExternalProviders,
    string? ExternalLoginUrl);

public sealed record LoginRequest(
    string? Username,
    string? Password,
    bool RememberLogin,
    string? Action,
    string? ReturnUrl);
