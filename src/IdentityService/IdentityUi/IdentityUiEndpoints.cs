using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using IdentityService.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using UiTelemetry = IdentityService.IdentityUi.IdentityUiTelemetry;

namespace IdentityService.IdentityUi;

internal static class IdentityUiEndpoints
{
    private const string ApiPrefix = "/api/identity-ui";
    private const string LocalProvider = IdentityServerConstants.LocalIdentityProvider;
    private const string InvalidCredentials = "Invalid username or password";

    public static IEndpointRouteBuilder MapIdentityUi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(ApiPrefix)
            .WithTags("Identity UI");

        api.MapGet("/session", GetSession).Produces<SessionResponse>().AllowAnonymous();
        api.MapGet("/antiforgery", GetAntiforgery).Produces<AntiforgeryResponse>().AllowAnonymous();
        api.MapGet("/error", GetError).Produces<ErrorContextResponse>().AllowAnonymous();

        api.MapGet("/login-context", GetLoginContext).Produces<LoginContextResponse>().AllowAnonymous();
        api.MapPost("/login", Login).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapGet("/register-context", GetRegisterContext).Produces<RegisterContextResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapPost("/register", Register).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();

        api.MapGet("/logout-context", GetLogoutContext).Produces<LogoutContextResponse>().AllowAnonymous();
        api.MapPost("/logout", Logout).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapGet("/logged-out-context", GetLoggedOutContext).Produces<LoggedOutContextResponse>().AllowAnonymous();

        api.MapGet("/consent-context", GetConsentContext).Produces<ConsentContextResponse>().RequireAuthorization();
        api.MapPost("/consent", SubmitConsent).Produces<NavigationResponse>().ProducesValidationProblem().RequireAuthorization();

        api.MapGet("/grants", GetGrants).Produces<IReadOnlyCollection<GrantDto>>().RequireAuthorization();
        api.MapDelete("/grants/{clientId}", RevokeGrant).Produces(StatusCodes.Status204NoContent).RequireAuthorization();

        endpoints.MapGet("/Home/Error/Index", (string? errorId) =>
                Results.Redirect($"/Error{(string.IsNullOrEmpty(errorId) ? string.Empty : $"?errorId={Uri.EscapeDataString(errorId)}")}"))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static IResult GetSession(HttpContext context)
    {
        NoStore(context);
        return Results.Ok(new SessionResponse(
            context.User.Identity?.IsAuthenticated == true,
            context.User.GetDisplayName()));
    }

    private static IResult GetAntiforgery(HttpContext context, IAntiforgery antiforgery)
    {
        NoStore(context);
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new AntiforgeryResponse(
            tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery request token was not generated."),
            tokens.HeaderName ?? "X-CSRF-TOKEN"));
    }

    private static async Task<IResult> GetError(
        HttpContext context,
        IIdentityServerInteractionService interaction,
        IWebHostEnvironment environment,
        string? errorId,
        CancellationToken cancellationToken)
    {
        NoStore(context);
        var error = await interaction.GetErrorContextAsync(errorId, cancellationToken);
        return Results.Ok(new ErrorContextResponse(
            error?.Error ?? "An unexpected identity error occurred.",
            environment.IsDevelopment() ? error?.ErrorDescription : null,
            error?.RequestId));
    }

    private static async Task<IResult> GetLoginContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var context = await interaction.GetAuthorizationContextAsync(returnUrl, cancellationToken);
        return Results.Ok(new LoginContextResponse(
            returnUrl,
            context?.LoginHint,
            context?.Client?.EnableLocalLogin ?? true,
            AllowRememberLogin: true));
    }

    private static async Task<IResult> Login(
        HttpContext httpContext,
        LoginRequest request,
        IAntiforgery antiforgery,
        IIdentityServerInteractionService interaction,
        IEventService events,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }

        var authorizationContext = await interaction.GetAuthorizationContextAsync(request.ReturnUrl, cancellationToken);

        if (!string.Equals(request.Action, "login", StringComparison.OrdinalIgnoreCase))
        {
            if (authorizationContext != null)
            {
                await interaction.DenyAuthorizationAsync(authorizationContext, InteractionError.AccessDenied, cancellationToken);
                return NavigationForAuthorizationContext(request.ReturnUrl);
            }

            return Navigation("/");
        }

        var validation = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            validation["username"] = ["Username is required."];
        }
        if (string.IsNullOrEmpty(request.Password))
        {
            validation["password"] = ["Password is required."];
        }
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var result = await signInManager.PasswordSignInAsync(
            request.Username!,
            request.Password!,
            request.RememberLogin,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            const string error = "invalid credentials";
            await events.RaiseAsync(new UserLoginFailureEvent(
                request.Username,
                error,
                clientId: authorizationContext?.Client.ClientId), cancellationToken);
            UiTelemetry.Metrics.UserLoginFailure(authorizationContext?.Client.ClientId, LocalProvider, error);
            return Results.ValidationProblem(Errors("credentials", InvalidCredentials));
        }

        var user = await userManager.FindByNameAsync(request.Username!);
        await events.RaiseAsync(new UserLoginSuccessEvent(
            user!.UserName,
            user.Id,
            user.UserName,
            clientId: authorizationContext?.Client.ClientId), cancellationToken);
        UiTelemetry.Metrics.UserLogin(authorizationContext?.Client.ClientId, LocalProvider);

        if (authorizationContext != null)
        {
            return NavigationForAuthorizationContext(request.ReturnUrl);
        }

        if (string.IsNullOrWhiteSpace(request.ReturnUrl))
        {
            return Navigation("/");
        }

        if (!IsLocalUrl(request.ReturnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "The return URL is invalid."));
        }

        return Navigation(request.ReturnUrl);
    }

    private static async Task<IResult> GetRegisterContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        IOptions<IdentityOptions> identityOptions,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var authorizationContext =
                await interaction.GetAuthorizationContextAsync(returnUrl, cancellationToken);
            if (authorizationContext == null && !IsLocalUrl(returnUrl))
            {
                return Results.ValidationProblem(Errors("returnUrl", "The return URL is invalid."));
            }
        }

        var password = identityOptions.Value.Password;
        return Results.Ok(new RegisterContextResponse(
            returnUrl,
            new PasswordRequirementsDto(
                password.RequiredLength,
                password.RequiredUniqueChars,
                password.RequireDigit,
                password.RequireLowercase,
                password.RequireUppercase,
                password.RequireNonAlphanumeric)));
    }

    private static async Task<IResult> Register(
        HttpContext httpContext,
        RegisterRequest request,
        IAntiforgery antiforgery,
        IIdentityServerInteractionService interaction,
        IEventService events,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }

        var authorizationContext =
            await interaction.GetAuthorizationContextAsync(request.ReturnUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ReturnUrl) &&
            authorizationContext == null &&
            !IsLocalUrl(request.ReturnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "The return URL is invalid."));
        }

        var validation = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var username = request.Username?.Trim();
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            validation["username"] = ["Username is required."];
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            validation["email"] = ["Email is required."];
        }
        else if (!new EmailAddressAttribute().IsValid(email))
        {
            validation["email"] = ["Enter a valid email address."];
        }
        if (string.IsNullOrEmpty(request.Password))
        {
            validation["password"] = ["Password is required."];
        }
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            validation["confirmPassword"] = ["Passwords do not match."];
        }
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = email
        };
        var result = await userManager.CreateAsync(user, request.Password!);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(MapIdentityErrors(result.Errors));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        await events.RaiseAsync(new UserLoginSuccessEvent(
            user.UserName,
            user.Id,
            user.UserName,
            clientId: authorizationContext?.Client.ClientId), cancellationToken);
        UiTelemetry.Metrics.UserLogin(authorizationContext?.Client.ClientId, LocalProvider);

        if (authorizationContext != null)
        {
            return NavigationForAuthorizationContext(request.ReturnUrl);
        }

        return Navigation(string.IsNullOrWhiteSpace(request.ReturnUrl) ? "/" : request.ReturnUrl);
    }

    private static async Task<IResult> GetLogoutContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        string? logoutId,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var showPrompt = true;
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            showPrompt = false;
        }
        else
        {
            var context = await interaction.GetLogoutContextAsync(logoutId, cancellationToken);
            if (context?.ShowSignoutPrompt == false)
            {
                showPrompt = false;
            }
        }

        return Results.Ok(new LogoutContextResponse(logoutId, showPrompt, !showPrompt));
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        IEventService events,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var logoutId = form["logoutId"].FirstOrDefault();

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            logoutId ??= await interaction.CreateLogoutContextAsync(cancellationToken);
            var idp = httpContext.User.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
            var subjectId = httpContext.User.GetSubjectId();
            var displayName = httpContext.User.GetDisplayName();

            await signInManager.SignOutAsync();
            await events.RaiseAsync(new UserLogoutSuccessEvent(subjectId, displayName), cancellationToken);
            UiTelemetry.Metrics.UserLogout(idp);
        }

        return Results.Redirect(LoggedOutUrl(logoutId));
    }

    private static async Task<IResult> GetLoggedOutContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        string? logoutId,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var logout = await interaction.GetLogoutContextAsync(logoutId, cancellationToken);
        return Results.Ok(new LoggedOutContextResponse(
            string.IsNullOrWhiteSpace(logout?.ClientName) ? logout?.ClientId : logout.ClientName,
            logout?.PostLogoutRedirectUri,
            logout?.SignOutIFrameUrl,
            AutomaticRedirectAfterSignOut: false));
    }

    private static async Task<IResult> GetConsentContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "A return URL is required."));
        }

        var request = await interaction.GetAuthorizationContextAsync(returnUrl, cancellationToken);
        if (request == null)
        {
            return Results.NotFound();
        }

        var scopes = BuildConsentScopes(request);
        return Results.Ok(new ConsentContextResponse(
            returnUrl,
            Client(request.Client),
            request.Client.AllowRememberConsent,
            scopes.Identity,
            scopes.Api));
    }

    private static async Task<IResult> SubmitConsent(
        HttpContext httpContext,
        ConsentRequest input,
        IAntiforgery antiforgery,
        IIdentityServerInteractionService interaction,
        IEventService events,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }
        if (string.IsNullOrWhiteSpace(input.ReturnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "A return URL is required."));
        }

        var request = await interaction.GetAuthorizationContextAsync(input.ReturnUrl, cancellationToken);
        if (request == null)
        {
            return Results.NotFound();
        }

        ConsentResponse response;
        if (string.Equals(input.Action, "no", StringComparison.OrdinalIgnoreCase))
        {
            response = new ConsentResponse { Error = InteractionError.AccessDenied };
            await events.RaiseAsync(new ConsentDeniedEvent(
                httpContext.User.GetSubjectId(),
                request.Client.ClientId,
                request.ValidatedResources.RawScopeValues), cancellationToken);
            UiTelemetry.Metrics.ConsentDenied(
                request.Client.ClientId,
                request.ValidatedResources.ParsedScopes.Select(scope => scope.ParsedName));
        }
        else if (string.Equals(input.Action, "yes", StringComparison.OrdinalIgnoreCase))
        {
            var scopeValidation = ValidateScopes(BuildConsentScopes(request), input.ScopesConsented);
            if (!scopeValidation.IsValid)
            {
                return Results.ValidationProblem(Errors("scopesConsented", scopeValidation.Error!));
            }

            response = new ConsentResponse
            {
                RememberConsent = input.RememberConsent,
                ScopesValuesConsented = scopeValidation.Scopes,
                Description = input.Description
            };

            await events.RaiseAsync(new ConsentGrantedEvent(
                httpContext.User.GetSubjectId(),
                request.Client.ClientId,
                request.ValidatedResources.RawScopeValues,
                response.ScopesValuesConsented,
                response.RememberConsent), cancellationToken);
            UiTelemetry.Metrics.ConsentGranted(
                request.Client.ClientId,
                response.ScopesValuesConsented,
                response.RememberConsent);
            var denied = request.ValidatedResources.ParsedScopes
                .Select(scope => scope.ParsedName)
                .Except(response.ScopesValuesConsented);
            UiTelemetry.Metrics.ConsentDenied(request.Client.ClientId, denied);
        }
        else
        {
            return Results.ValidationProblem(Errors("action", "Invalid consent selection."));
        }

        await interaction.GrantConsentAsync(request, response, cancellationToken);
        return NavigationForAuthorizationContext(input.ReturnUrl);
    }

    private static async Task<IResult> GetGrants(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        IClientStore clients,
        IResourceStore resources,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var grants = await interaction.GetAllUserGrantsAsync(cancellationToken);
        var response = new List<GrantDto>();
        foreach (var grant in grants)
        {
            var client = await clients.FindClientByIdAsync(grant.ClientId, cancellationToken);
            if (client == null)
            {
                continue;
            }

            var grantedResources = await resources.FindResourcesByScopeAsync(grant.Scopes, cancellationToken);
            response.Add(new GrantDto(
                client.ClientId,
                client.ClientName ?? client.ClientId,
                client.ClientUri,
                client.LogoUri,
                grant.Description,
                grant.CreationTime,
                grant.Expiration,
                grantedResources.IdentityResources.Select(resource => resource.DisplayName ?? resource.Name).ToArray(),
                grantedResources.ApiScopes.Select(scope => scope.DisplayName ?? scope.Name).ToArray()));
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> RevokeGrant(
        HttpContext httpContext,
        string clientId,
        IAntiforgery antiforgery,
        IIdentityServerInteractionService interaction,
        IEventService events,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.ValidationProblem(Errors("clientId", "A client ID is required."));
        }

        var current = await interaction.GetAllUserGrantsAsync(cancellationToken);
        if (!current.Any(grant => string.Equals(grant.ClientId, clientId, StringComparison.Ordinal)))
        {
            return Results.NotFound();
        }

        await interaction.RevokeUserConsentAsync(clientId, cancellationToken);
        await events.RaiseAsync(new GrantsRevokedEvent(httpContext.User.GetSubjectId(), clientId), cancellationToken);
        UiTelemetry.Metrics.GrantsRevoked(clientId);
        return Results.NoContent();
    }

    private static ScopeSet BuildConsentScopes(AuthorizationRequest request)
    {
        var resourceIndicators = request.Parameters.GetValues(OidcConstants.AuthorizeRequest.Resource) ?? [];
        var apiResources = request.ValidatedResources.Resources.ApiResources
            .Where(resource => resourceIndicators.Contains(resource.Name));
        return BuildScopes(request.ValidatedResources, apiResources);
    }

    private static ScopeSet BuildScopes(
        ResourceValidationResult validated,
        IEnumerable<ApiResource> apiResources)
    {
        var identity = validated.Resources.IdentityResources
            .Select(resource => new ScopeDto(
                resource.Name,
                resource.Name,
                resource.DisplayName ?? resource.Name,
                resource.Description,
                resource.Emphasize,
                resource.Required,
                true,
                []))
            .ToArray();

        var resources = apiResources.ToArray();
        var api = new List<ScopeDto>();
        foreach (var parsed in validated.ParsedScopes)
        {
            var scope = validated.Resources.FindApiScope(parsed.ParsedName);
            if (scope == null)
            {
                continue;
            }

            var displayName = scope.DisplayName ?? scope.Name;
            if (!string.IsNullOrWhiteSpace(parsed.ParsedParameter))
            {
                displayName += $":{parsed.ParsedParameter}";
            }

            api.Add(new ScopeDto(
                parsed.ParsedName,
                parsed.RawValue,
                displayName,
                scope.Description,
                scope.Emphasize,
                scope.Required,
                true,
                resources
                    .Where(resource => resource.Scopes.Contains(parsed.ParsedName))
                    .Select(resource => new ResourceDto(resource.Name, resource.DisplayName ?? resource.Name))
                    .ToArray()));
        }

        if (validated.Resources.OfflineAccess)
        {
            api.Add(new ScopeDto(
                IdentityServerConstants.StandardScopes.OfflineAccess,
                IdentityServerConstants.StandardScopes.OfflineAccess,
                "Offline Access",
                "Access to your applications and resources, even when you are offline",
                true,
                false,
                true,
                []));
        }

        return new ScopeSet(identity, api);
    }

    private static ScopeValidation ValidateScopes(
        ScopeSet scopes,
        IReadOnlyCollection<string>? selected)
    {
        var all = scopes.Identity.Concat(scopes.Api).ToArray();
        var allowed = all.Select(scope => scope.Value).ToHashSet(StringComparer.Ordinal);
        var normalized = (selected ?? [])
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (normalized.Any(scope => !allowed.Contains(scope)))
        {
            return new ScopeValidation(false, [], "One or more selected permissions are invalid.");
        }

        normalized.UnionWith(all.Where(scope => scope.Required).Select(scope => scope.Value));
        if (normalized.Count == 0)
        {
            return new ScopeValidation(false, [], "You must pick at least one permission.");
        }

        return new ScopeValidation(true, normalized.ToArray(), null);
    }

    private static ClientContextDto Client(Client client) =>
        new(client.ClientName ?? client.ClientId, client.ClientUri, client.LogoUri);

    private static IResult NavigationForAuthorizationContext(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl)
            ? Results.ValidationProblem(Errors("returnUrl", "A return URL is required."))
            : Navigation(returnUrl);

    private static IResult Navigation(string url) =>
        Results.Ok(new NavigationResponse(new NavigationDto("redirect", url)));

    private static string LoggedOutUrl(string? logoutId) =>
        $"/Account/Logout/LoggedOut{(string.IsNullOrWhiteSpace(logoutId) ? string.Empty : $"?logoutId={Uri.EscapeDataString(logoutId)}")}";

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }
        return url[0] == '/' &&
               (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) ||
               url.Length > 1 &&
               url[0] == '~' &&
               url[1] == '/';
    }

    private static async Task<bool> IsAntiforgeryValid(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult AntiforgeryProblem() =>
        Results.Problem(
            "The request could not be verified. Refresh the page and try again.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid antiforgery token");

    private static Dictionary<string, string[]> Errors(string key, string message) =>
        new(StringComparer.Ordinal) { [key] = [message] };

    private static Dictionary<string, string[]> MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var mapped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var error in errors)
        {
            var key = error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)
                ? "password"
                : error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase)
                    ? "email"
                    : error.Code.Contains("User", StringComparison.OrdinalIgnoreCase)
                        ? "username"
                        : "registration";
            if (!mapped.TryGetValue(key, out var messages))
            {
                messages = [];
                mapped[key] = messages;
            }
            messages.Add(error.Description);
        }

        return mapped.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void NoStore(HttpContext context) =>
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";

    private sealed record ScopeSet(
        IReadOnlyCollection<ScopeDto> Identity,
        IReadOnlyCollection<ScopeDto> Api);

    private sealed record ScopeValidation(
        bool IsValid,
        IReadOnlyCollection<string> Scopes,
        string? Error);
}
