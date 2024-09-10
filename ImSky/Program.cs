using System.Reflection;
using ImSky.Api;
using ImSky.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ImSky;

public class Program {
    public static string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImSky");
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
    public static IHost Host = null!;

    public static void Main() {
        var overrideAppDir = Environment.GetEnvironmentVariable("IMSKY_APPDIR");
        if (overrideAppDir is not null) AppDir = overrideAppDir;
        if (!Directory.Exists(AppDir)) Directory.CreateDirectory(AppDir);

        var config = Config.Load();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(AppDir, "ImSky.log"))
            .MinimumLevel.Debug()
            .CreateLogger();

        var builder = new HostApplicationBuilder {
            Environment = {
                ContentRootPath = AppDir
            }
        };
        builder.Services.AddSerilog();

        builder.Services.AddSingleton(config);
        builder.Services.AddSingletonHostedService<GuiService>();

        builder.Services.AddSingleton<AtProtoService>();
        builder.Services.AddSingleton<FeedService>();
        builder.Services.AddSingleton<InteractionService>();
        builder.Services.AddSingleton<UsersService>();

        builder.Services.AddSingleton<LoginView>();
        builder.Services.AddSingleton<FeedsView>();
        builder.Services.AddSingleton<PostView>();
        builder.Services.AddSingleton<WriteView>();
        builder.Services.AddSingleton<UserView>();

        Host = builder.Build();
        Host.Start();
        Host.WaitForShutdown();
    }
}
