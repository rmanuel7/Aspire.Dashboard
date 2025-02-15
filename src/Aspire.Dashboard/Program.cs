namespace Aspire.Dashboard;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var startup = new Startup(configuration: builder.Configuration);

        startup.ConfigureServices(services: builder.Services);

        startup.ConfigureOptions(builder: builder);

        var app = builder.Build();

        startup.Configure(app: app, env: app.Environment);

        app.Run();
    }
}
