namespace IdentityService.IdentityUi;

public sealed record PasswordRequirementsDto(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric);

public sealed record RegisterContextResponse(
    string? ReturnUrl,
    PasswordRequirementsDto PasswordRequirements);

public sealed record RegisterRequest(
    string? Username,
    string? Email,
    string? Password,
    string? ConfirmPassword,
    string? ReturnUrl);
