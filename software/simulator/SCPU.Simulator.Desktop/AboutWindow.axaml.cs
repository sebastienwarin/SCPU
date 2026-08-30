using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using SCPU.Simulator.Desktop.Infrastructure;

namespace SCPU.Simulator.Desktop;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"Version {BuildInfo.Version}";
    }

    private void OnOpenBuildACPU(object? sender, RoutedEventArgs eventArgs) =>
        BrowserLauncher.Open("https://buildacpu.com");

    private void OnOpenAuthorWebsite(object? sender, RoutedEventArgs eventArgs) =>
        BrowserLauncher.Open("https://sebastien.warin.fr");

    private void OnClose(object? sender, RoutedEventArgs eventArgs) => Close();

    private void OnDragAreaPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var source = eventArgs.Source as Control;
        if (source is Button || source?.FindAncestorOfType<Button>() is not null) return;
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(eventArgs);
    }

    private void OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape) return;
        eventArgs.Handled = true;
        Close();
    }
}
