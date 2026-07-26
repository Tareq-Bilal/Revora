namespace IdentityService.IdentityUi;

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
