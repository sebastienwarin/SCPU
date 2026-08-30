using System.Diagnostics;

namespace SCPU.Simulator.Desktop.Infrastructure;

internal static class BrowserLauncher
{
    public static void Open(string address) =>
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
}
