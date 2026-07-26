using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using IdentityService.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        api.MapGet("/home", GetHome).Produces<HomeResponse>().AllowAnonymous();
        api.MapGet("/error", GetError).Produces<ErrorContextResponse>().AllowAnonymous();
        api.MapGet("/redirect-context", GetRedirectContext).Produces<RedirectContextResponse>().AllowAnonymous();

        api.MapGet("/login-context", GetLoginContext).Produces<LoginContextResponse>().AllowAnonymous();
        api.MapPost("/login", Login).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapGet("/register-context", GetRegisterContext).Produces<RegisterContextResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapPost("/register", Register).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();

        api.MapGet("/logout-context", GetLogoutContext).Produces<LogoutContextResponse>().AllowAnonymous();
        api.MapPost("/logout", Logout).Produces<NavigationResponse>().ProducesValidationProblem().AllowAnonymous();
        api.MapGet("/logged-out-context", GetLoggedOutContext).Produces<LoggedOutContextResponse>().AllowAnonymous();

        api.MapGet("/consent-context", GetConsentContext).Produces<ConsentContextResponse>().RequireAuthorization();
        api.MapPost("/consent", SubmitConsent).Produces<NavigationResponse>().ProducesValidationProblem().RequireAuthorization();

        api.MapGet("/device-context", GetDeviceContext).Produces<DeviceContextResponse>().RequireAuthorization();
        api.MapPost("/device", SubmitDevice).Produces<NavigationResponse>().ProducesValidationProblem().RequireAuthorization();

        api.MapGet("/ciba/request", GetCibaRequest).Produces<CibaRequestResponse>().AllowAnonymous();
        api.MapGet("/ciba/pending", GetPendingCibaRequests).Produces<IReadOnlyCollection<PendingCibaRequestDto>>().RequireAuthorization();
        api.MapGet("/ciba/consent-context", GetCibaConsentContext).Produces<CibaConsentContextResponse>().RequireAuthorization();
        api.MapPost("/ciba/consent", SubmitCibaConsent).Produces<NavigationResponse>().ProducesValidationProblem().RequireAuthorization();

        api.MapGet("/grants", GetGrants).Produces<IReadOnlyCollection<GrantDto>>().RequireAuthorization();
        api.MapDelete("/grants/{clientId}", RevokeGrant).Produces(StatusCodes.Status204NoContent).RequireAuthorization();

        api.MapGet("/diagnostics", (Delegate)GetDiagnostics).Produces<DiagnosticsResponse>().RequireAuthorization();
        api.MapGet("/sessions", GetSessions).Produces<SessionsResponse>().RequireAuthorization();
        api.MapDelete("/sessions/{sessionId}", DeleteSession).Produces(StatusCodes.Status204NoContent).RequireAuthorization();

        endpoints.MapGet("/ExternalLogin/Challenge", ChallengeExternalLogin)
            .AllowAnonymous()
            .ExcludeFromDescription();
        endpoints.MapGet("/ExternalLogin/Callback", async (
                HttpContext httpContext,
                IIdentityServerInteractionService interaction,
                IEventService events,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            await ExternalLoginCallback(
                httpContext,
                interaction,
                events,
                userManager,
                signInManager,
                loggerFactory,
                cancellationToken))
            .AllowAnonymous()
            .ExcludeFromDescription();
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
            context.User.GetDisplayName(),
            !context.Connection.IsRemote()));
    }

    private static IResult GetAntiforgery(HttpContext context, IAntiforgery antiforgery)
    {
        NoStore(context);
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new AntiforgeryResponse(
            tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery request token was not generated."),
            tokens.HeaderName ?? "X-CSRF-TOKEN"));
    }

    private static IResult GetHome(HttpContext context)
    {
        NoStore(context);
        var license = context.RequestServices.GetService<LicenseInformation>();
        var version = typeof(Duende.IdentityServer.Hosting.IdentityServerMiddleware).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+').First()
            ?? "unavailable";

        return Results.Ok(new HomeResponse(
            version,
            license?.IsConfigured == true,
            license?.SerialNumber.ToString(),
            license?.Expiration));
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

    private static IResult GetRedirectContext(
        HttpContext context,
        IIdentityServerInteractionService interaction,
        string? redirectUri)
    {
        NoStore(context);
        if (!IsLocalUrl(redirectUri) || !interaction.IsValidReturnUrl(redirectUri))
        {
            return Results.ValidationProblem(Errors("redirectUri", "The redirect URL is invalid."));
        }

        return Results.Ok(new RedirectContextResponse(redirectUri!));
    }

    private static async Task<IResult> GetLoginContext(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemeProvider,
        IIdentityProviderStore identityProviderStore,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var model = await BuildLoginContext(
            interaction,
            schemeProvider,
            identityProviderStore,
            returnUrl,
            cancellationToken);
        return Results.Ok(model);
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
                return NavigationForAuthorizationContext(authorizationContext, request.ReturnUrl);
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
            return NavigationForAuthorizationContext(authorizationContext, request.ReturnUrl);
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
            return NavigationForAuthorizationContext(authorizationContext, request.ReturnUrl);
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

            if (!string.IsNullOrEmpty(idp) &&
                idp != LocalProvider &&
                await httpContext.GetSchemeSupportsSignOutAsync(idp))
            {
                var redirectUri = LoggedOutUrl(logoutId);
                return Results.SignOut(new AuthenticationProperties { RedirectUri = redirectUri }, [idp]);
            }
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

        return Results.Ok(BuildConsentContext(request, returnUrl));
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
        return NavigationForAuthorizationContext(request, input.ReturnUrl);
    }

    private static async Task<IResult> GetDeviceContext(
        HttpContext httpContext,
        IDeviceFlowInteractionService interaction,
        string? userCode,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (string.IsNullOrWhiteSpace(userCode))
        {
            return Results.Ok(new DeviceContextResponse(
                null,
                false,
                null,
                null,
                [],
                []));
        }

        var request = await interaction.GetAuthorizationContextAsync(userCode, cancellationToken);
        if (request == null)
        {
            return Results.Ok(new DeviceContextResponse(
                userCode,
                false,
                "Invalid user code",
                null,
                [],
                []));
        }

        var scopes = BuildDeviceScopes(request);
        return Results.Ok(new DeviceContextResponse(
            userCode,
            true,
            null,
            Client(request.Client),
            scopes.Identity,
            scopes.Api));
    }

    private static async Task<IResult> SubmitDevice(
        HttpContext httpContext,
        DeviceRequest input,
        IAntiforgery antiforgery,
        IDeviceFlowInteractionService interaction,
        IEventService events,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }
        if (string.IsNullOrWhiteSpace(input.UserCode))
        {
            return Results.ValidationProblem(Errors("userCode", "A user code is required."));
        }

        var request = await interaction.GetAuthorizationContextAsync(input.UserCode, cancellationToken);
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
            var validation = ValidateScopes(BuildDeviceScopes(request), input.ScopesConsented);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(Errors("scopesConsented", validation.Error!));
            }

            response = new ConsentResponse
            {
                RememberConsent = false,
                ScopesValuesConsented = validation.Scopes,
                Description = input.Description
            };
            await events.RaiseAsync(new ConsentGrantedEvent(
                httpContext.User.GetSubjectId(),
                request.Client.ClientId,
                request.ValidatedResources.RawScopeValues,
                response.ScopesValuesConsented,
                false), cancellationToken);
            UiTelemetry.Metrics.ConsentGranted(request.Client.ClientId, response.ScopesValuesConsented, false);
        }
        else
        {
            return Results.ValidationProblem(Errors("action", "Invalid device authorization selection."));
        }

        await interaction.HandleRequestAsync(input.UserCode, response, cancellationToken);
        return Navigation("/Device/Success");
    }

    private static async Task<IResult> GetCibaRequest(
        HttpContext httpContext,
        IBackchannelAuthenticationInteractionService interaction,
        string? id,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.ValidationProblem(Errors("id", "A backchannel login ID is required."));
        }

        var request = await interaction.GetLoginRequestByInternalIdAsync(id, cancellationToken);
        return request == null
            ? Results.NotFound()
            : Results.Ok(new CibaRequestResponse(id, Client(request.Client), request.BindingMessage));
    }

    private static async Task<IResult> GetPendingCibaRequests(
        HttpContext httpContext,
        IBackchannelAuthenticationInteractionService interaction,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        var requests = await interaction.GetPendingLoginRequestsForCurrentUserAsync(cancellationToken);
        return Results.Ok(requests.Select(request => new PendingCibaRequestDto(
            request.InternalId,
            request.Client.ClientId,
            request.Client.ClientName,
            request.BindingMessage)).ToArray());
    }

    private static async Task<IResult> GetCibaConsentContext(
        HttpContext httpContext,
        IBackchannelAuthenticationInteractionService interaction,
        string? id,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.ValidationProblem(Errors("id", "A backchannel login ID is required."));
        }

        var request = await interaction.GetLoginRequestByInternalIdAsync(id, cancellationToken);
        if (request == null || request.Subject.GetSubjectId() != httpContext.User.GetSubjectId())
        {
            return Results.NotFound();
        }

        var scopes = BuildCibaScopes(request);
        return Results.Ok(new CibaConsentContextResponse(
            id,
            Client(request.Client),
            request.BindingMessage,
            scopes.Identity,
            scopes.Api));
    }

    private static async Task<IResult> SubmitCibaConsent(
        HttpContext httpContext,
        CibaConsentRequest input,
        IAntiforgery antiforgery,
        IBackchannelAuthenticationInteractionService interaction,
        IEventService events,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            return Results.ValidationProblem(Errors("id", "A backchannel login ID is required."));
        }

        var request = await interaction.GetLoginRequestByInternalIdAsync(input.Id, cancellationToken);
        if (request == null || request.Subject.GetSubjectId() != httpContext.User.GetSubjectId())
        {
            return Results.NotFound();
        }

        CompleteBackchannelLoginRequest response;
        if (string.Equals(input.Action, "no", StringComparison.OrdinalIgnoreCase))
        {
            response = new CompleteBackchannelLoginRequest(input.Id);
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
            var validation = ValidateScopes(BuildCibaScopes(request), input.ScopesConsented);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(Errors("scopesConsented", validation.Error!));
            }

            response = new CompleteBackchannelLoginRequest(input.Id)
            {
                ScopesValuesConsented = validation.Scopes,
                Description = input.Description
            };
            await events.RaiseAsync(new ConsentGrantedEvent(
                httpContext.User.GetSubjectId(),
                request.Client.ClientId,
                request.ValidatedResources.RawScopeValues,
                response.ScopesValuesConsented,
                false), cancellationToken);
            UiTelemetry.Metrics.ConsentGranted(request.Client.ClientId, response.ScopesValuesConsented, false);
        }
        else
        {
            return Results.ValidationProblem(Errors("action", "Invalid backchannel authorization selection."));
        }

        await interaction.CompleteLoginRequestAsync(response, cancellationToken);
        return Navigation("/Ciba/All");
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

    private static async Task<IResult> GetDiagnostics(HttpContext httpContext)
    {
        NoStore(httpContext);
        if (httpContext.Connection.IsRemote())
        {
            return Results.NotFound();
        }

        var result = await httpContext.AuthenticateAsync();
        var claims = result.Principal?.Claims
            .Select(claim => new DiagnosticsItemDto(claim.Type, claim.Value))
            .ToArray() ?? [];
        var properties = result.Properties?.Items
            .Select(item => new DiagnosticsItemDto(item.Key, item.Value))
            .ToArray() ?? [];

        var clients = Array.Empty<string>();
        if (result.Properties?.Items.TryGetValue("client_list", out var encoded) == true &&
            !string.IsNullOrWhiteSpace(encoded))
        {
            var bytes = Base64Url.DecodeFromChars(encoded);
            clients = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(bytes)) ?? [];
        }

        return Results.Ok(new DiagnosticsResponse(claims, properties, clients));
    }

    private static async Task<IResult> GetSessions(
        HttpContext httpContext,
        string? displayName,
        string? sessionId,
        string? subjectId,
        string? token,
        bool previous,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (httpContext.Connection.IsRemote())
        {
            return Results.NotFound();
        }

        var service = httpContext.RequestServices.GetService<ISessionManagementService>();
        if (service == null)
        {
            return Results.Ok(new SessionsResponse(false, [], null, false, false, null, null, null));
        }

        var query = await service.QuerySessionsAsync(new SessionQuery
        {
            ResultsToken = token,
            RequestPriorResults = previous,
            DisplayName = displayName,
            SessionId = sessionId,
            SubjectId = subjectId
        }, cancellationToken);

        var sessions = query.Results.Select(session => new UserSessionDto(
            session.SubjectId,
            session.SessionId,
            session.DisplayName,
            session.Created,
            session.Expires,
            session.ClientIds?.ToArray() ?? [])).ToArray();

        return Results.Ok(new SessionsResponse(
            true,
            sessions,
            query.ResultsToken,
            query.HasPrevResults,
            query.HasNextResults,
            query.TotalCount,
            query.CurrentPage,
            query.TotalPages));
    }

    private static async Task<IResult> DeleteSession(
        HttpContext httpContext,
        string sessionId,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        NoStore(httpContext);
        if (httpContext.Connection.IsRemote())
        {
            return Results.NotFound();
        }
        if (!await IsAntiforgeryValid(httpContext, antiforgery))
        {
            return AntiforgeryProblem();
        }

        var service = httpContext.RequestServices.GetService<ISessionManagementService>();
        if (service == null)
        {
            return Results.Problem("Server-side sessions are not enabled.", statusCode: StatusCodes.Status409Conflict);
        }

        await service.RemoveSessionsAsync(new RemoveSessionsContext { SessionId = sessionId }, cancellationToken);
        return Results.NoContent();
    }

    private static IResult ChallengeExternalLogin(
        string scheme,
        string? returnUrl,
        IIdentityServerInteractionService interaction)
    {
        returnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        if (!IsLocalUrl(returnUrl) && !interaction.IsValidReturnUrl(returnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "The return URL is invalid."));
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/ExternalLogin/Callback",
            Items =
            {
                ["returnUrl"] = returnUrl,
                ["scheme"] = scheme
            }
        };

        return Results.Challenge(properties, [scheme]);
    }

    private static async Task<IResult> ExternalLoginCallback(
        HttpContext httpContext,
        IIdentityServerInteractionService interaction,
        IEventService events,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var result = await httpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        if (result.Succeeded != true)
        {
            return Results.Problem($"External authentication error: {result.Failure?.Message}");
        }

        var externalUser = result.Principal;
        if (externalUser == null)
        {
            return Results.Problem("External authentication produced no user.");
        }

        var logger = loggerFactory.CreateLogger("IdentityService.IdentityUi.ExternalLogin");
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "External claims received: {ClaimTypes}",
                externalUser.Claims.Select(claim => claim.Type).ToArray());
        }

        var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                          externalUser.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Results.Problem("The external provider did not return a user identifier.");
        }

        if (result.Properties?.Items.TryGetValue("scheme", out var provider) != true ||
            string.IsNullOrWhiteSpace(provider))
        {
            return Results.Problem("The external authentication scheme was not preserved.");
        }

        var user = await userManager.FindByLoginAsync(provider, userIdClaim.Value);
        if (user == null)
        {
            user = await AutoProvisionUser(
                provider,
                userIdClaim.Value,
                externalUser.Claims.ToList(),
                userManager);
        }

        var localClaims = new List<Claim>
        {
            new(JwtClaimTypes.IdentityProvider, provider)
        };
        var localProperties = new AuthenticationProperties();
        var sessionId = externalUser.FindFirst(JwtClaimTypes.SessionId);
        if (sessionId != null)
        {
            localClaims.Add(new Claim(JwtClaimTypes.SessionId, sessionId.Value));
        }
        var idToken = result.Properties?.GetTokenValue("id_token");
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            localProperties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        await signInManager.SignInWithClaimsAsync(user, localProperties, localClaims);
        await httpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        var returnUrl = result.Properties?.Items["returnUrl"] ?? "/";
        var authorizationContext = await interaction.GetAuthorizationContextAsync(returnUrl, cancellationToken);
        await events.RaiseAsync(new UserLoginSuccessEvent(
            provider,
            userIdClaim.Value,
            user.Id,
            user.UserName,
            true,
            authorizationContext?.Client.ClientId), cancellationToken);
        UiTelemetry.Metrics.UserLogin(authorizationContext?.Client.ClientId, provider);

        var destination = authorizationContext?.IsNativeClient() == true
            ? NativeLoadingUrl(returnUrl)
            : returnUrl;
        return Results.Redirect(destination);
    }

    private static async Task<ApplicationUser> AutoProvisionUser(
        string provider,
        string providerUserId,
        List<Claim> claims,
        UserManager<ApplicationUser> userManager)
    {
        claims.RemoveAll(claim => claim.Type is JwtClaimTypes.Subject or ClaimTypes.NameIdentifier);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = Guid.NewGuid().ToString(),
            Email = claims.FirstOrDefault(claim => claim.Type is JwtClaimTypes.Email or ClaimTypes.Email)?.Value
        };

        var name = claims.FirstOrDefault(claim => claim.Type is JwtClaimTypes.Name or ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            var first = claims.FirstOrDefault(claim => claim.Type is JwtClaimTypes.GivenName or ClaimTypes.GivenName)?.Value;
            var last = claims.FirstOrDefault(claim => claim.Type is JwtClaimTypes.FamilyName or ClaimTypes.Surname)?.Value;
            name = string.Join(' ', new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Errors.First().Description);
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            result = await userManager.AddClaimAsync(user, new Claim(JwtClaimTypes.Name, name));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Errors.First().Description);
            }
        }

        result = await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerUserId, provider));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Errors.First().Description);
        }

        return user;
    }

    private static async Task<LoginContextResponse> BuildLoginContext(
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemeProvider,
        IIdentityProviderStore identityProviderStore,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var context = await interaction.GetAuthorizationContextAsync(returnUrl, cancellationToken);
        var username = context?.LoginHint;
        var providers = new List<(string Scheme, string DisplayName)>();
        var enableLocalLogin = true;

        if (!string.IsNullOrWhiteSpace(context?.IdP))
        {
            var scheme = await schemeProvider.GetSchemeAsync(context.IdP);
            if (scheme != null)
            {
                var local = context.IdP == LocalProvider;
                enableLocalLogin = local;
                if (!local)
                {
                    providers.Add((context.IdP, scheme.DisplayName ?? context.IdP));
                }
            }
        }
        else
        {
            providers.AddRange((await schemeProvider.GetAllSchemesAsync())
                .Where(scheme => scheme.DisplayName != null)
                .Select(scheme => (scheme.Name, scheme.DisplayName ?? scheme.Name)));
            providers.AddRange((await identityProviderStore.GetAllSchemeNamesAsync(cancellationToken))
                .Where(scheme => scheme.Enabled)
                .Select(scheme => (scheme.Scheme, scheme.DisplayName ?? scheme.Scheme)));

            if (context?.Client != null)
            {
                enableLocalLogin = context.Client.EnableLocalLogin;
                if (context.Client.IdentityProviderRestrictions?.Count > 0)
                {
                    providers = providers
                        .Where(provider => context.Client.IdentityProviderRestrictions.Contains(provider.Scheme))
                        .ToList();
                }
            }
        }

        var externalProviders = providers
            .DistinctBy(provider => provider.Scheme)
            .Select(provider => new ExternalProviderDto(
                provider.Scheme,
                provider.DisplayName,
                ExternalChallengeUrl(provider.Scheme, returnUrl)))
            .ToArray();
        var externalOnly = !enableLocalLogin && externalProviders.Length == 1
            ? externalProviders[0].ChallengeUrl
            : null;

        return new LoginContextResponse(
            returnUrl,
            username,
            enableLocalLogin,
            true,
            externalProviders,
            externalOnly);
    }

    private static ConsentContextResponse BuildConsentContext(AuthorizationRequest request, string returnUrl)
    {
        var scopes = BuildConsentScopes(request);
        return new ConsentContextResponse(
            returnUrl,
            Client(request.Client),
            request.Client.AllowRememberConsent,
            scopes.Identity,
            scopes.Api);
    }

    private static ScopeSet BuildConsentScopes(AuthorizationRequest request)
    {
        var resourceIndicators = request.Parameters.GetValues(OidcConstants.AuthorizeRequest.Resource) ?? [];
        var apiResources = request.ValidatedResources.Resources.ApiResources
            .Where(resource => resourceIndicators.Contains(resource.Name));
        return BuildScopes(
            request.ValidatedResources,
            apiResources,
            includeOfflineAccess: true);
    }

    private static ScopeSet BuildDeviceScopes(DeviceFlowAuthorizationRequest request) =>
        BuildScopes(request.ValidatedResources, [], includeOfflineAccess: true);

    private static ScopeSet BuildCibaScopes(BackchannelUserLoginRequest request)
    {
        var indicators = request.RequestedResourceIndicators ?? [];
        var apiResources = request.ValidatedResources.Resources.ApiResources
            .Where(resource => indicators.Contains(resource.Name));
        return BuildScopes(request.ValidatedResources, apiResources, includeOfflineAccess: true);
    }

    private static ScopeSet BuildScopes(
        ResourceValidationResult validated,
        IEnumerable<ApiResource> apiResources,
        bool includeOfflineAccess)
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

        if (includeOfflineAccess && validated.Resources.OfflineAccess)
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

    private static IResult NavigationForAuthorizationContext(AuthorizationRequest context, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.ValidationProblem(Errors("returnUrl", "A return URL is required."));
        }
        return context.IsNativeClient()
            ? Navigation(NativeLoadingUrl(returnUrl), "native-loading")
            : Navigation(returnUrl);
    }

    private static IResult Navigation(string url, string kind = "redirect") =>
        Results.Ok(new NavigationResponse(new NavigationDto(kind, url)));

    private static string NativeLoadingUrl(string returnUrl) =>
        $"/Redirect?redirectUri={Uri.EscapeDataString(returnUrl)}";

    private static string ExternalChallengeUrl(string scheme, string? returnUrl) =>
        $"/ExternalLogin/Challenge?scheme={Uri.EscapeDataString(scheme)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";

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
