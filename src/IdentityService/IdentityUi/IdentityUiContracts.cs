namespace IdentityService.IdentityUi;

public sealed record AntiforgeryResponse(string RequestToken, string HeaderName);

public sealed record SessionResponse(
    bool IsAuthenticated,
    string? DisplayName,
    bool IsLocalRequest);

public sealed record NavigationDto(string Kind, string Url);

public sealed record NavigationResponse(NavigationDto Navigation);

public sealed record HomeResponse(
    string Version,
    bool LicenseConfigured,
    string? LicenseSerialNumber,
    DateTimeOffset? LicenseExpiration);

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

public sealed record LogoutContextResponse(
    string? LogoutId,
    bool ShowPrompt,
    bool AutoSubmit);

public sealed record LoggedOutContextResponse(
    string? ClientName,
    string? PostLogoutRedirectUri,
    string? SignOutIframeUrl,
    bool AutomaticRedirectAfterSignOut);

public sealed record ClientContextDto(
    string? ClientName,
    string? ClientUrl,
    string? ClientLogoUrl);

public sealed record ResourceDto(string? Name, string? DisplayName);

public sealed record ScopeDto(
    string? Name,
    string Value,
    string DisplayName,
    string? Description,
    bool Emphasize,
    bool Required,
    bool Checked,
    IReadOnlyCollection<ResourceDto> Resources);

public sealed record ConsentContextResponse(
    string ReturnUrl,
    ClientContextDto Client,
    bool AllowRememberConsent,
    IReadOnlyCollection<ScopeDto> IdentityScopes,
    IReadOnlyCollection<ScopeDto> ApiScopes);

public sealed record ConsentRequest(
    string? ReturnUrl,
    string? Action,
    IReadOnlyCollection<string>? ScopesConsented,
    bool RememberConsent,
    string? Description);

public sealed record DeviceContextResponse(
    string? UserCode,
    bool HasRequest,
    string? Error,
    ClientContextDto? Client,
    IReadOnlyCollection<ScopeDto> IdentityScopes,
    IReadOnlyCollection<ScopeDto> ApiScopes);

public sealed record DeviceRequest(
    string? UserCode,
    string? Action,
    IReadOnlyCollection<string>? ScopesConsented,
    string? Description);

public sealed record CibaRequestResponse(
    string Id,
    ClientContextDto Client,
    string? BindingMessage);

public sealed record PendingCibaRequestDto(
    string Id,
    string ClientId,
    string? ClientName,
    string? BindingMessage);

public sealed record CibaConsentContextResponse(
    string Id,
    ClientContextDto Client,
    string? BindingMessage,
    IReadOnlyCollection<ScopeDto> IdentityScopes,
    IReadOnlyCollection<ScopeDto> ApiScopes);

public sealed record CibaConsentRequest(
    string? Id,
    string? Action,
    IReadOnlyCollection<string>? ScopesConsented,
    string? Description);

public sealed record GrantDto(
    string ClientId,
    string ClientName,
    string? ClientUrl,
    string? ClientLogoUrl,
    string? Description,
    DateTime Created,
    DateTime? Expires,
    IReadOnlyCollection<string> IdentityGrantNames,
    IReadOnlyCollection<string> ApiGrantNames);

public sealed record DiagnosticsItemDto(string Name, string? Value);

public sealed record DiagnosticsResponse(
    IReadOnlyCollection<DiagnosticsItemDto> Claims,
    IReadOnlyCollection<DiagnosticsItemDto> Properties,
    IReadOnlyCollection<string> Clients);

public sealed record UserSessionDto(
    string SubjectId,
    string SessionId,
    string? DisplayName,
    DateTime Created,
    DateTime? Expires,
    IReadOnlyCollection<string> ClientIds);

public sealed record SessionsResponse(
    bool Enabled,
    IReadOnlyCollection<UserSessionDto> Results,
    string? ResultsToken,
    bool HasPreviousResults,
    bool HasNextResults,
    int? TotalCount,
    int? CurrentPage,
    int? TotalPages);

public sealed record ErrorContextResponse(
    string Error,
    string? ErrorDescription,
    string? RequestId);

public sealed record RedirectContextResponse(string RedirectUri);
