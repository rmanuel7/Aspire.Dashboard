using Aspire.Dashboard.Components;
using Aspire.Dashboard.Otlp;
using Aspire.Dashboard.Otlp.Grpc;
using Aspire.Dashboard.Otlp.Http;

namespace Aspire.Dashboard;

public class Startup
{
    public IConfiguration Configuration { get; }
    public IServiceCollection Services { get; private set; }

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
        services.AddTransient<OtlpLogsService>();
        services.AddTransient<OtlpTraceService>();
        services.AddTransient<OtlpMetricsService>();
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
}
