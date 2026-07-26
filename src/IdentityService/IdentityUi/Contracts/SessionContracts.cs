namespace IdentityService.IdentityUi;

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
