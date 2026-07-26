namespace IdentityService.IdentityUi;

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
