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

public sealed record ErrorContextResponse(
    string Error,
    string? ErrorDescription,
    string? RequestId);

public sealed record RedirectContextResponse(string RedirectUri);
