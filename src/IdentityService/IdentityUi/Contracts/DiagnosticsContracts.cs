namespace IdentityService.IdentityUi;

public sealed record DiagnosticsItemDto(string Name, string? Value);

public sealed record DiagnosticsResponse(
    IReadOnlyCollection<DiagnosticsItemDto> Claims,
    IReadOnlyCollection<DiagnosticsItemDto> Properties,
    IReadOnlyCollection<string> Clients);
