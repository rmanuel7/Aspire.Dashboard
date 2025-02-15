using Aspire.Dashboard.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Configuration;

public sealed class PostConfigureDashboardOptions : IPostConfigureOptions<DashboardOptions>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public PostConfigureDashboardOptions(IConfiguration configuration) : this(configuration, NullLogger<PostConfigureDashboardOptions>.Instance)
    {
    }

    public PostConfigureDashboardOptions(IConfiguration configuration, ILogger<PostConfigureDashboardOptions> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void PostConfigure(string? name, DashboardOptions options)
    {
        _logger.LogDebug($"PostConfigure {nameof(DashboardOptions)} with name '{name}'.");

        // Copy aliased config values to the strongly typed options.
        if (_configuration[DashboardConfigNames.DashboardOtlpGrpcUrlName.ConfigKey] is { Length: > 0 } otlpGrpcUrl)
        {
            options.Otlp.GrpcEndpointUrl = otlpGrpcUrl;
        }
        //else
        //{
        //    options.Otlp.GrpcEndpointUrl = "http://localhost:18889";
        //    _configuration[DashboardConfigNames.DashboardOtlpGrpcUrlName.ConfigKey] = options.Otlp.GrpcEndpointUrl;
        //}

        // Copy aliased config values to the strongly typed options.
        if (_configuration[DashboardConfigNames.DashboardOtlpHttpUrlName.ConfigKey] is { Length: > 0 } otlpHttpUrl)
        {
            options.Otlp.HttpEndpointUrl = otlpHttpUrl;
        }
        //else
        //{
        //    options.Otlp.HttpEndpointUrl = "http://localhost:18890";
        //    _configuration[DashboardConfigNames.DashboardOtlpHttpUrlName.ConfigKey] = options.Otlp.HttpEndpointUrl;
        //}

        if (_configuration[DashboardConfigNames.DashboardFrontendUrlName.ConfigKey] is { Length: > 0 } frontendUrls)
        {
            options.Frontend.EndpointUrls = frontendUrls;
        }
        //else
        //{
        //    options.Frontend.EndpointUrls = "http://localhost:18888";
        //    _configuration[DashboardConfigNames.DashboardFrontendUrlName.ConfigKey] = options.Frontend.EndpointUrls;
        //}

        if (_configuration[DashboardConfigNames.ResourceServiceUrlName.ConfigKey] is { Length: > 0 } resourceServiceUrl)
        {
            options.ResourceServiceClient.Url = resourceServiceUrl;
        }

        if (_configuration.GetBool(DashboardConfigNames.DashboardUnsecuredAllowAnonymousName.ConfigKey) ?? false)
        {
            options.Frontend.AuthMode = FrontendAuthMode.Unsecured;
            options.Otlp.AuthMode = OtlpAuthMode.Unsecured;
        }
        else
        {
            options.Frontend.AuthMode ??= FrontendAuthMode.BrowserToken;
            options.Otlp.AuthMode ??= OtlpAuthMode.Unsecured;
        }

        if (options.Frontend.AuthMode == FrontendAuthMode.BrowserToken && string.IsNullOrEmpty(options.Frontend.BrowserToken))
        {
            var token = TokenGenerator.GenerateToken();

            // Set the generated token in configuration. This is required because options could be created multiple times
            // (at startup, after CI is created, after options change). Setting the token in configuration makes it consistent.
            _configuration[DashboardConfigNames.DashboardFrontendBrowserTokenName.ConfigKey] = token;

            options.Frontend.BrowserToken = token;
        }
    }
}
