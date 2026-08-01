using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityService.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityService;

public sealed class RevoraProfileService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory) : IProfileService
{
    public async Task GetProfileDataAsync(
        ProfileDataRequestContext context,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(context.Subject);
        if (user == null)
        {
            return;
        }

        var principal = await claimsFactory.CreateAsync(user);
        var claims = principal.Claims.ToList();

        AddIfMissing(claims, JwtClaimTypes.Name, user.UserName);
        AddIfMissing(claims, JwtClaimTypes.PreferredUserName, user.UserName);

        if (user.EmailConfirmed)
        {
            AddIfMissing(claims, JwtClaimTypes.Email, user.Email);
        }

        context.AddRequestedClaims(claims);
    }

    public async Task IsActiveAsync(
        IsActiveContext context,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(context.Subject);
        if (user == null || !await signInManager.CanSignInAsync(user))
        {
            context.IsActive = false;
            return;
        }

        context.IsActive =
            !userManager.SupportsUserLockout ||
            !await userManager.IsLockedOutAsync(user);
    }

    private async Task<ApplicationUser?> FindUserAsync(ClaimsPrincipal subject)
    {
        var subjectId = subject.GetSubjectId();
        return string.IsNullOrWhiteSpace(subjectId)
            ? null
            : await userManager.FindByIdAsync(subjectId);
    }

    private static void AddIfMissing(
        ICollection<Claim> claims,
        string claimType,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            claims.All(claim => claim.Type != claimType))
        {
            claims.Add(new Claim(claimType, value));
        }
    }
}
