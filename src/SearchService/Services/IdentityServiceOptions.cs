namespace SearchService.Services;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    public string Authority { get; init; }
    public string Audience { get; init; }
    public string ClientId { get; init; }
    public string ClientSecret { get; init; }
    public string Scope { get; init; }
}
