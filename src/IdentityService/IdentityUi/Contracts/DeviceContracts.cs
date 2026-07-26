namespace IdentityService.IdentityUi;

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
