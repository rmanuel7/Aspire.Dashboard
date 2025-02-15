using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Aspire.Dashboard.Components;
using Aspire.Dashboard.Configuration;
using Aspire.Dashboard.Otlp;
using Aspire.Dashboard.Otlp.Grpc;
using Aspire.Dashboard.Otlp.Http;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard;

public class Startup
{
    // Campos
    private IConfigurationSection _dashboardConfigSection;
    private WebApplication _app;
    private IOptionsMonitor<DashboardOptions> _dashboardOptionsMonitor;
    private IReadOnlyList<string> _validationFailures;
    private ILogger<Startup> _logger;
    private DashboardOptions _dashboardOptions;

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

            _dashboardOptions = dashboardOptions;

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

    public void Configure(WebApplication app, IWebHostEnvironment env)
    {
        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        // +
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // OTLP HTTP services.
        app.MapHttpOtlpApi(/*dashboardOptions.Otlp*/);

        // OTLP gRPC services.
        app.MapGrpcService<OtlpGrpcTraceService>();
        app.MapGrpcService<OtlpGrpcMetricsService>();
        app.MapGrpcService<OtlpGrpcLogsService>();
    }

    /// <summary>
    /// Load <see cref="DashboardOptions"/> from configuration without using DI. This performs
    /// the same steps as getting the options from DI but without the need for a service provider.
    /// </summary>
    private static bool TryGetDashboardOptions(WebApplicationBuilder builder, IConfigurationSection dashboardConfigSection, [NotNullWhen(true)] out DashboardOptions? dashboardOptions, [NotNullWhen(false)] out IEnumerable<string>? failureMessages)
    {
        dashboardOptions = new DashboardOptions();

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

    private static void WriteValidationFailures(ILogger<Startup> logger, IReadOnlyList<string> validationFailures)
    {
        logger.LogError("Failed to start the dashboard due to {Count} configuration error(s).", validationFailures.Count);
        foreach (var message in validationFailures)
        {
            logger.LogError("{ErrorMessage}", message);
        }
    }
}
