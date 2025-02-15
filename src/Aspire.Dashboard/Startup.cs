using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Claims;
using Aspire.Dashboard.Authentication;
using Aspire.Dashboard.Components;
using Aspire.Dashboard.Configuration;
using Aspire.Dashboard.Otlp;
using Aspire.Dashboard.Otlp.Grpc;
using Aspire.Dashboard.Otlp.Http;
using Aspire.Dashboard.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Aspire.Dashboard;

public class Startup
{
    private const string DASHBOARD_AUTH_COOKIE_NAME = ".Aspire.Dashboard.Auth";

    // Campos
    private IConfigurationSection _dashboardConfigSection;
    private WebApplication _app;
    private IOptionsMonitor<DashboardOptions> _dashboardOptionsMonitor;
    private IReadOnlyList<string> _validationFailures;
    private ILogger<Startup> _logger;
    private DashboardOptions _dashboardOptions;
    private readonly List<Func<EndpointInfo>> _frontendEndPointAccessor = new();
    private Func<EndpointInfo>? _otlpServiceGrpcEndPointAccessor;
    private Func<EndpointInfo>? _otlpServiceHttpEndPointAccessor;
    private bool _isAllHttps;

    // Propiedades
    public IConfiguration Configuration { get; }
    public IServiceCollection Services { get; private set; }
    public IServiceProvider ServiceProvider { get; private set; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        Services = services;

        // Add services to the container.
        Services.AddGrpc();

        Services.AddRazorComponents()
            .AddInteractiveServerComponents();


        // OTLP services.
        Services.AddTransient<OtlpLogsService>();
        Services.AddTransient<OtlpTraceService>();
        Services.AddTransient<OtlpMetricsService>();
    }

    public void ConfigureOptions(WebApplicationBuilder builder)
    {
        // Configuration
        _dashboardConfigSection = Configuration.GetSection("Dashboard");

        Services.AddOptions<DashboardOptions>()
            .Bind(_dashboardConfigSection)
            .ValidateOnStart();

        Services.AddSingleton<IPostConfigureOptions<DashboardOptions>, PostConfigureDashboardOptions>();
        Services.AddSingleton<IValidateOptions<DashboardOptions>, ValidateDashboardOptions>();

        if (!TryGetDashboardOptions(builder, _dashboardConfigSection, out var dashboardOptions, out var failureMessages))
        {
            // The options have validation failures. Write them out to the user and return a non-zero exit code.
            // We don't want to start the app, but we need to build the app to access the logger to log the errors.
            _app = builder.Build();

            _dashboardOptionsMonitor = _app.Services.GetRequiredService<IOptionsMonitor<DashboardOptions>>();

            _validationFailures = failureMessages.ToList();

            _logger = GetLogger();

            ServiceProvider = _app.Services;

            WriteVersion(_logger);

            WriteValidationFailures(_logger, _validationFailures);

            throw new OptionsValidationException(
                optionsName: nameof(DashboardOptions),
                optionsType: typeof(DashboardOptions),
                failureMessages: failureMessages);
        }
        else
        {
            _validationFailures = Array.Empty<string>();
        }
    }

    // Kestrel endpoints are loaded from configuration. This is done so that advanced configuration of endpoints is
    // possible from the caller. e.g., using environment variables to configure each endpoint's TLS certificate.
    public void ConfigureKestrelEndpoints(WebApplicationBuilder builder)
    {
        // A single endpoint is configured if URLs are the same and the port isn't dynamic.
        var frontendAddresses = _dashboardOptions.Frontend.GetEndpointAddresses();
        var otlpGrpcAddress = _dashboardOptions.Otlp.GetGrpcEndpointAddress();
        var otlpHttpAddress = _dashboardOptions.Otlp.GetHttpEndpointAddress();
        var hasSingleEndpoint = frontendAddresses.Count == 1 && IsSameOrNull(frontendAddresses[0], otlpGrpcAddress) && IsSameOrNull(frontendAddresses[0], otlpHttpAddress);

        var initialValues = new Dictionary<string, string?>();
        var browserEndpointNames = new List<string>(capacity: frontendAddresses.Count);

        if (!hasSingleEndpoint)
        {
            // Translate high-level config settings such as DOTNET_DASHBOARD_OTLP_ENDPOINT_URL and ASPNETCORE_URLS
            // to Kestrel's schema for loading endpoints from configuration.
            if (otlpGrpcAddress != null)
            {
                AddEndpointConfiguration(initialValues, "OtlpGrpc", otlpGrpcAddress.ToString(), HttpProtocols.Http2, requiredClientCertificate: _dashboardOptions.Otlp.AuthMode == OtlpAuthMode.ClientCertificate);
            }

            if (otlpHttpAddress != null)
            {
                AddEndpointConfiguration(initialValues, "OtlpHttp", otlpHttpAddress.ToString(), HttpProtocols.Http1AndHttp2, requiredClientCertificate: _dashboardOptions.Otlp.AuthMode == OtlpAuthMode.ClientCertificate);
            }

            if (frontendAddresses.Count == 1)
            {
                browserEndpointNames.Add("Browser");
                AddEndpointConfiguration(initialValues, "Browser", frontendAddresses[0].ToString());
            }
            else
            {
                for (var i = 0; i < frontendAddresses.Count; i++)
                {
                    var name = $"Browser{i}";
                    browserEndpointNames.Add(name);
                    AddEndpointConfiguration(initialValues, name, frontendAddresses[i].ToString());
                }
            }
        }
        else
        {
            // At least one gRPC endpoint must be present.
            var url = otlpGrpcAddress?.ToString() ?? otlpHttpAddress?.ToString();

            AddEndpointConfiguration(initialValues, "OtlpGrpc", url!, HttpProtocols.Http1AndHttp2, requiredClientCertificate: _dashboardOptions.Otlp.AuthMode == OtlpAuthMode.ClientCertificate);
        }

        builder.Configuration.AddInMemoryCollection(initialValues);

        // Use ConfigurationLoader to augment the endpoints that Kestrel created from configuration
        // with extra settings. e.g., UseOtlpConnection for the OTLP endpoint.
        builder.WebHost.ConfigureKestrel((context, serverOptions) =>
        {
            var logger = serverOptions.ApplicationServices.GetRequiredService<ILogger<Startup>>();

            var kestrelSection = context.Configuration.GetSection("Kestrel");
            var configurationLoader = serverOptions.Configure(kestrelSection);

            foreach (var browserEndpointName in browserEndpointNames)
            {
                configurationLoader.Endpoint(name: browserEndpointName, configureOptions: endpointConfiguration =>
                {
                    endpointConfiguration.ListenOptions.UseConnectionTypes([ConnectionType.Frontend]);

                    // Only the last endpoint is accessible. Tests should only need one but
                    // this will need to be improved if that changes.
                    _frontendEndPointAccessor.Add(CreateEndPointAccessor(endpointConfiguration));
                });
            }

            configurationLoader.Endpoint("OtlpGrpc", endpointConfiguration =>
            {
                var connectionTypes = new List<ConnectionType> { ConnectionType.Otlp };

                _otlpServiceGrpcEndPointAccessor ??= CreateEndPointAccessor(endpointConfiguration);

                if (hasSingleEndpoint)
                {
                    logger.LogDebug("Browser and OTLP accessible on a single endpoint.");

                    if (!endpointConfiguration.IsHttps)
                    {
                        logger.LogWarning(
                            "The dashboard is configured with a shared endpoint for browser access and the OTLP service. " +
                            "The endpoint doesn't use TLS so browser access is only possible via a TLS terminating proxy.");
                    }

                    connectionTypes.Add(ConnectionType.Frontend);

                    _frontendEndPointAccessor.Add(_otlpServiceGrpcEndPointAccessor);
                }

                endpointConfiguration.ListenOptions.UseConnectionTypes(connectionTypes.ToArray());

                if (endpointConfiguration.HttpsOptions.ClientCertificateMode == ClientCertificateMode.RequireCertificate)
                {
                    // Allow invalid certificates when creating the connection. Certificate validation is done in the auth middleware.
                    endpointConfiguration.HttpsOptions.ClientCertificateValidation = (certificate, chain, sslPolicyErrors) =>
                    {
                        return true;
                    };
                }
            });

            configurationLoader.Endpoint("OtlpHttp", endpointConfiguration =>
            {
                var connectionTypes = new List<ConnectionType> { ConnectionType.Otlp };

                _otlpServiceHttpEndPointAccessor ??= CreateEndPointAccessor(endpointConfiguration);

                if (hasSingleEndpoint)
                {
                    logger.LogDebug("Browser and OTLP accessible on a single endpoint.");

                    if (!endpointConfiguration.IsHttps)
                    {
                        logger.LogWarning(
                            "The dashboard is configured with a shared endpoint for browser access and the OTLP service. " +
                            "The endpoint doesn't use TLS so browser access is only possible via a TLS terminating proxy.");
                    }

                    connectionTypes.Add(ConnectionType.Frontend);

                    _frontendEndPointAccessor.Add(_otlpServiceHttpEndPointAccessor);
                }

                endpointConfiguration.ListenOptions.UseConnectionTypes(connectionTypes.ToArray());

                if (endpointConfiguration.HttpsOptions.ClientCertificateMode == ClientCertificateMode.RequireCertificate)
                {
                    // Allow invalid certificates when creating the connection. Certificate validation is done in the auth middleware.
                    endpointConfiguration.HttpsOptions.ClientCertificateValidation = (certificate, chain, sslPolicyErrors) => { return true; };
                }
            });

        });

        var browserHttpsPort = frontendAddresses.FirstOrDefault(IsHttpsOrNull)?.Port;

        _isAllHttps = browserHttpsPort is not null && IsHttpsOrNull(otlpGrpcAddress) && IsHttpsOrNull(otlpHttpAddress);

        if (_isAllHttps)
        {
            // Explicitly configure the HTTPS redirect port as we're possibly listening on multiple HTTPS addresses
            // if the dashboard OTLP URL is configured to use HTTPS too
            builder.Services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = browserHttpsPort);
        }
    }

    public void ConfigureAuthentication(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPolicyEvaluator, AspirePolicyEvaluator>();

        var authentication = builder.Services
            // ASP.NET Core authentication needs to have the correct default scheme for the configured frontend auth.
            // This is required for ASP.NET Core/SignalR/Blazor to flow the authenticated user from the request and into the dashboard app.
            .AddAuthentication(configureOptions: o => o.DefaultScheme = _dashboardOptions.Frontend.AuthMode switch
            {
                FrontendAuthMode.Unsecured => FrontendAuthenticationDefaults.AuthenticationSchemeUnsecured,
                _ => CookieAuthenticationDefaults.AuthenticationScheme
            })

            .AddScheme<FrontendCompositeAuthenticationHandlerOptions, FrontendCompositeAuthenticationHandler>(
                authenticationScheme: FrontendCompositeAuthenticationDefaults.AuthenticationScheme,
                configureOptions: o => { })
            // AuthenticationTicket - OtlpAuthorization.OtlpClaimName
            .AddScheme<OtlpCompositeAuthenticationHandlerOptions, OtlpCompositeAuthenticationHandler>(
                authenticationScheme: OtlpCompositeAuthenticationDefaults.AuthenticationScheme,
                configureOptions: o => { })
            // OtlpAuthMode.ApiKey - "x-otlp-api-key"
            .AddScheme<OtlpApiKeyAuthenticationHandlerOptions, OtlpApiKeyAuthenticationHandler>(
                authenticationScheme: OtlpApiKeyAuthenticationDefaults.AuthenticationScheme,
                configureOptions: o => { })

            .AddScheme<ConnectionTypeAuthenticationHandlerOptions, ConnectionTypeAuthenticationHandler>(
                authenticationScheme: ConnectionTypeAuthenticationDefaults.AuthenticationSchemeFrontend,
                configureOptions: o => o.RequiredConnectionType = ConnectionType.Frontend)

            .AddScheme<ConnectionTypeAuthenticationHandlerOptions, ConnectionTypeAuthenticationHandler>(
                authenticationScheme: ConnectionTypeAuthenticationDefaults.AuthenticationSchemeOtlp,
                configureOptions: o => o.RequiredConnectionType = ConnectionType.Otlp);


        switch (_dashboardOptions.Frontend.AuthMode)
        {
            case FrontendAuthMode.OpenIdConnect:

                authentication.AddPolicyScheme(
                    authenticationScheme: FrontendAuthenticationDefaults.AuthenticationSchemeOpenIdConnect,
                    displayName: FrontendAuthenticationDefaults.AuthenticationSchemeOpenIdConnect,
                    configureOptions: o =>
                    {
                        // The frontend authentication scheme just redirects to OpenIdConnect and Cookie schemes, as appropriate.
                        o.ForwardDefault = CookieAuthenticationDefaults.AuthenticationScheme;
                        o.ForwardChallenge = OpenIdConnectDefaults.AuthenticationScheme;
                    });

                authentication.AddCookie(configureOptions: options =>
                {
                    options.Cookie.Name = DASHBOARD_AUTH_COOKIE_NAME;
                });

                authentication.AddOpenIdConnect(configureOptions: options =>
                {
                    // Use authorization code flow so clients don't see access tokens.
                    options.ResponseType = OpenIdConnectResponseType.Code;

                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                    // Scopes "openid" and "profile" are added by default, but need to be re-added
                    // in case configuration exists for Authentication:Schemes:OpenIdConnect:Scope.
                    if (!options.Scope.Contains(OpenIdConnectScope.OpenId))
                    {
                        options.Scope.Add(OpenIdConnectScope.OpenId);
                    }

                    if (!options.Scope.Contains("profile"))
                    {
                        options.Scope.Add("profile");
                    }

                    // Redirect to resources upon sign-in.
                    options.CallbackPath = "/"; // TargetLocationInterceptor.ResourcesPath;

                    // Avoid "message.State is null or empty" due to use of CallbackPath above.
                    options.SkipUnrecognizedRequests = true;
                });

                break;

            case FrontendAuthMode.BrowserToken:

                authentication.AddPolicyScheme(
                    authenticationScheme: FrontendAuthenticationDefaults.AuthenticationSchemeBrowserToken,
                    displayName: FrontendAuthenticationDefaults.AuthenticationSchemeBrowserToken,
                    configureOptions: o =>
                    {
                        o.ForwardDefault = CookieAuthenticationDefaults.AuthenticationScheme;
                    });

                authentication.AddCookie(configureOptions: options =>
                {
                    options.Cookie.Name = DASHBOARD_AUTH_COOKIE_NAME;
                    options.LoginPath = "/login";
                    options.ReturnUrlParameter = "returnUrl";
                    options.ExpireTimeSpan = TimeSpan.FromDays(3);
                    options.Events.OnSigningIn = context =>
                    {
                        // Add claim when signing in with cookies from browser token.
                        // Authorization requires this claim. This prevents an identity from another auth scheme from being allow.
                        var claimsIdentity = (ClaimsIdentity)context.Principal!.Identity!;

                        claimsIdentity.AddClaim(
                            new Claim(
                                type: FrontendAuthorizationDefaults.BrowserTokenClaimName,
                                value: bool.TrueString));

                        return Task.CompletedTask;
                    };
                });

                break;

            case FrontendAuthMode.Unsecured:
                // AuthenticationTicket
                authentication.AddScheme<AuthenticationSchemeOptions, UnsecuredAuthenticationHandler>(
                    authenticationScheme: FrontendAuthenticationDefaults.AuthenticationSchemeUnsecured,
                    configureOptions: o => { });

                break;
        }

        builder.Services.AddAuthorization(configure: options =>
        {
            options.AddPolicy(
                name: OtlpAuthorization.PolicyName,
                policy: new AuthorizationPolicyBuilder(OtlpCompositeAuthenticationDefaults.AuthenticationScheme)
                    .RequireClaim(OtlpAuthorization.OtlpClaimName, [bool.TrueString])
                    .Build());

            switch (_dashboardOptions.Frontend.AuthMode)
            {
                case FrontendAuthMode.OpenIdConnect:

                    options.AddPolicy(
                        name: FrontendAuthorizationDefaults.PolicyName,
                        policy: new AuthorizationPolicyBuilder(FrontendCompositeAuthenticationDefaults.AuthenticationScheme)
                            .RequireOpenIdClaims(options: _dashboardOptions.Frontend.OpenIdConnect)
                            .Build());

                    break;

                case FrontendAuthMode.BrowserToken:

                    options.AddPolicy(
                        name: FrontendAuthorizationDefaults.PolicyName,
                        policy: new AuthorizationPolicyBuilder(FrontendCompositeAuthenticationDefaults.AuthenticationScheme)
                            .RequireClaim(FrontendAuthorizationDefaults.BrowserTokenClaimName)
                            .Build());

                    break;

                case FrontendAuthMode.Unsecured:

                    options.AddPolicy(
                        name: FrontendAuthorizationDefaults.PolicyName,
                        policy: new AuthorizationPolicyBuilder(FrontendCompositeAuthenticationDefaults.AuthenticationScheme)
                            .RequireClaim(FrontendAuthorizationDefaults.UnsecuredClaimName)
                            .Build());

                    break;

                default:

                    throw new NotSupportedException($"Unexpected {nameof(FrontendAuthMode)} enum member: {_dashboardOptions.Frontend.AuthMode}");
            }
        });
    }

    public void Configure(WebApplication app, IWebHostEnvironment env)
    {
        _app = app;

        _dashboardOptionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<DashboardOptions>>();

        _logger = GetLogger();

        WriteVersion(_logger);

        app.Lifetime.ApplicationStarted.Register(() =>
        {

            EndpointInfo? frontendEndpointInfo = null;

            if (_frontendEndPointAccessor.Count > 0)
            {
                if (_dashboardOptions.Otlp.Cors.IsCorsEnabled)
                {
                    var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

                    // Default policy allows the dashboard's origins.
                    // This is added so CORS middleware doesn't report failure for dashboard browser requests that include an origin header.
                    // Needs to be added once app is started so the resolved frontend endpoint can be used.
                    corsOptions.AddDefaultPolicy(builder =>
                    {
                        builder.WithOrigins(_frontendEndPointAccessor.Select(accessor => accessor().GetResolvedAddress()).ToArray());
                        builder.AllowAnyHeader();
                        builder.AllowAnyMethod();
                    });
                }

                frontendEndpointInfo = _frontendEndPointAccessor[0]();

                _logger.LogInformation("Now listening on: {DashboardUri}", frontendEndpointInfo.GetResolvedAddress());
            }

            if (_otlpServiceGrpcEndPointAccessor != null)
            {
                // This isn't used by dotnet watch but still useful to have for debugging
                _logger.LogInformation("OTLP/gRPC listening on: {OtlpEndpointUri}", _otlpServiceGrpcEndPointAccessor().GetResolvedAddress());
            }
            if (_otlpServiceHttpEndPointAccessor != null)
            {
                // This isn't used by dotnet watch but still useful to have for debugging
                _logger.LogInformation("OTLP/HTTP listening on: {OtlpEndpointUri}", _otlpServiceHttpEndPointAccessor().GetResolvedAddress());
            }

            if (_dashboardOptionsMonitor.CurrentValue.Otlp.AuthMode == OtlpAuthMode.Unsecured)
            {
                _logger.LogWarning("OTLP server is unsecured. Untrusted apps can send telemetry to the dashboard. For more information, visit https://go.microsoft.com/fwlink/?linkid=2267030");
            }

            // Log frontend login URL last at startup so it's easy to find in the logs.
            if (frontendEndpointInfo != null)
            {
                var options = app.Services.GetRequiredService<IOptionsMonitor<DashboardOptions>>().CurrentValue;

                if (options.Frontend.AuthMode == FrontendAuthMode.BrowserToken)
                {
                    var token = options.Frontend.BrowserToken;

                    var dashboardUrls = frontendEndpointInfo.GetResolvedAddress(replaceIPAnyWithLocalhost: true);

                    if (string.IsNullOrEmpty(token))
                    {
                        throw new InvalidOperationException("Token must be provided.");
                    }

                    if (StringUtils.TryGetUriFromDelimitedString(dashboardUrls, ";", out var firstDashboardUrl))
                    {
                        _logger.LogInformation("Login to the dashboard at {DashboardLoginUrl}", $"{firstDashboardUrl.GetLeftPart(UriPartial.Authority)}/login?t={token}");
                    }
                }
            }
        });

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // +
        app.UseAuthorization();
        app.UseAntiforgery();

        // +
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // OTLP HTTP services.
        app.MapHttpOtlpApi(options: _dashboardOptions.Otlp);

        // OTLP gRPC services.
        app.MapGrpcService<OtlpGrpcTraceService>();
        app.MapGrpcService<OtlpGrpcMetricsService>();
        app.MapGrpcService<OtlpGrpcLogsService>();
    }

    /// <summary>
    /// Load <see cref="DashboardOptions"/> from configuration without using DI. This performs
    /// the same steps as getting the options from DI but without the need for a service provider.
    /// </summary>
    private bool TryGetDashboardOptions(WebApplicationBuilder builder, IConfigurationSection dashboardConfigSection, [NotNullWhen(true)] out DashboardOptions? dashboardOptions, [NotNullWhen(false)] out IEnumerable<string>? failureMessages)
    {
        dashboardOptions = new DashboardOptions();

        _dashboardOptions = dashboardOptions;

        dashboardConfigSection.Bind(dashboardOptions);

        new PostConfigureDashboardOptions(builder.Configuration)
            .PostConfigure(name: string.Empty, dashboardOptions);

        var result = new ValidateDashboardOptions()
            .Validate(name: string.Empty, dashboardOptions);

        if (result.Failed)
        {
            failureMessages = result.Failures;
            return false;
        }
        else
        {
            failureMessages = null;
            return true;
        }
    }

    private void AddEndpointConfiguration(Dictionary<string, string?> values, string endpointName, string url, HttpProtocols? protocols = null, bool requiredClientCertificate = false)
    {
        values[$"Kestrel:Endpoints:{endpointName}:Url"] = url;

        if (protocols != null)
        {
            values[$"Kestrel:Endpoints:{endpointName}:Protocols"] = protocols.ToString();
        }

        if (requiredClientCertificate && IsHttpsOrNull(BindingAddress.Parse(url)))
        {
            values[$"Kestrel:Endpoints:{endpointName}:ClientCertificateMode"] = ClientCertificateMode.RequireCertificate.ToString();
        }
    }

    private Func<EndpointInfo> CreateEndPointAccessor(EndpointConfiguration endpointConfiguration)
    {
        // We want to provide a way for someone to get the IP address of an endpoint.
        // However, if a dynamic port is used, the port is not known until the server is started.
        // Instead of returning the ListenOption's endpoint directly, we provide a func that returns the endpoint.
        // The endpoint on ListenOptions is updated after binding, so accessing it via the func after the server
        // has started returns the resolved port.
        var address = BindingAddress.Parse(endpointConfiguration.ConfigSection["Url"]!);
        return () =>
        {
            var endpoint = endpointConfiguration.ListenOptions.IPEndPoint!;

            return new EndpointInfo(address, endpoint, endpointConfiguration.IsHttps);
        };
    }

    private ILogger<Startup> GetLogger()
    {
        return _app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Startup>();
    }

    private void WriteVersion(ILogger<Startup> logger)
    {
        if (typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is string informationalVersion)
        {
            // Write version at info level so it's written to the console by default. Help us debug user issues.
            // Display version and commit like 8.0.0-preview.2.23619.3+17dd83f67c6822954ec9a918ef2d048a78ad4697
            logger.LogInformation("Aspire version: {Version}", informationalVersion);
        }
    }

    private void WriteValidationFailures(ILogger<Startup> logger, IReadOnlyList<string> validationFailures)
    {
        logger.LogError("Failed to start the dashboard due to {Count} configuration error(s).", validationFailures.Count);
        foreach (var message in validationFailures)
        {
            logger.LogError("{ErrorMessage}", message);
        }
    }

    private bool IsSameOrNull(BindingAddress frontendAddress, BindingAddress? otlpAddress)
    {
        return otlpAddress == null || (frontendAddress.Equals(otlpAddress) && otlpAddress.Port != 0);
    }

    private bool IsHttpsOrNull(BindingAddress? address)
    {
        return address == null || string.Equals(address.Scheme, "https", StringComparison.Ordinal);
    }
}
