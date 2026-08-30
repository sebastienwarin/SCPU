using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SCPU.Simulator.Desktop;

public partial class GotoAddressWindow : Window
{
    private readonly Func<string, uint>? _resolver;

    public GotoAddressWindow() : this(null) { }

    public GotoAddressWindow(Func<string, uint>? resolver)
    {
        _resolver = resolver;
        InitializeComponent();
        Opened += (_, _) => AddressBox.Focus();
    }

    public GotoAddressWindow(Func<string, uint> resolver, string title, string prompt, string action)
        : this(resolver)
    {
        Title = title;
        PromptText.Text = prompt;
        AcceptButton.Content = action;
    }

    public string AddressText => AddressBox.Text?.Trim() ?? string.Empty;
    public uint? Address { get; private set; }

    public void ShowError(string message) => ErrorText.Text = message;

    private void OnAccept(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            Address = _resolver?.Invoke(AddressText);
            Close(Address);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            ShowError(exception.Message);
            AddressBox.SelectAll();
            AddressBox.Focus();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs eventArgs) => Close(null);
}
