using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using IdentityService.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityService;

public sealed class ResourceOwnerPasswordValidator(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IResourceOwnerPasswordValidator
{
    public async Task ValidateAsync(
        ResourceOwnerPasswordValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(context.UserName);
        if (user is null)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "invalid username or password");
            return;
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            context.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "invalid username or password");
            return;
        }

        context.Result = new GrantValidationResult(
            user.Id,
            OidcConstants.AuthenticationMethods.Password);
    }
}
