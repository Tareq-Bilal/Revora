namespace IdentityService.IdentityUi;

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
