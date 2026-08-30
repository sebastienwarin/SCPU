using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCPU.Simulator.Core;
using SCPU.Simulator.Debugger;
using SCPU.Simulator.Desktop.Infrastructure;
using SCPU.Simulator.Desktop.ViewModels;
using SCPU.Simulator.Devices;

namespace SCPU.Simulator.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
        builder.Services.AddSingleton<UiLogStore>();
        builder.Services.AddSingleton<ILoggerProvider, UiLoggerProvider>();
        builder.Services.AddSingleton(LaunchOptions.Parse(args));
        builder.Services.AddSingleton<DesktopSettingsStore>();
        builder.Services.AddSingleton<LedPanelDevice>();
        builder.Services.AddSingleton<BufferedTerminalDevice>();
        builder.Services.AddSCPUDebugger(provider =>
        {
            var processor = new Processor();
            processor.Devices.Add(DeviceId.Device0, provider.GetRequiredService<LedPanelDevice>());
            processor.Devices.Add(DeviceId.Device1, provider.GetRequiredService<BufferedTerminalDevice>());
            return processor;
        });
        builder.Services.AddSingleton<IDesktopFilePicker, DesktopFilePicker>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        using var host = builder.Build();
        App.Services = host.Services;
        host.Start();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
