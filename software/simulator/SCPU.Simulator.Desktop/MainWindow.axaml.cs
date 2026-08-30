using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using SCPU.Simulator.Desktop.ViewModels;
using SCPU.Simulator.Desktop.Infrastructure;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SCPU.Simulator.Desktop;

public partial class MainWindow : Window
{
    private const double CollapseDragDistance = 80;
    private readonly MainWindowViewModel _viewModel;
    private double? _cpuMinimumPointerX;
    private double? _cpuCollapsedPointerX;
    private double? _bottomMinimumPointerY;
    private double? _bottomCollapsedPointerY;
    private readonly Stack<int> _mainTabHistory = [];
    private int _currentMainTabIndex;
    private bool _isNavigatingBack;
    private bool _terminalScrollPending;

    public MainWindow() : this(App.Services.GetRequiredService<MainWindowViewModel>()) { }

    internal MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        CpuPanelSplitter.AddHandler(PointerMovedEvent, OnCpuSplitterPointerMoved, RoutingStrategies.Tunnel, true);
        CpuPanelSplitter.AddHandler(PointerReleasedEvent, OnPanelSplitterPointerReleased, RoutingStrategies.Tunnel, true);
        BottomPanelSplitter.AddHandler(PointerMovedEvent, OnBottomSplitterPointerMoved, RoutingStrategies.Tunnel, true);
        BottomPanelSplitter.AddHandler(PointerReleasedEvent, OnPanelSplitterPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, true);
        AssemblySourceList.AddHandler(PointerPressedEvent, OnAssemblySourcePointerPressed, RoutingStrategies.Tunnel);
        TerminalSurface.AddHandler(InputElement.TextInputEvent, OnTerminalTextInput,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Logs.CollectionChanged += OnLogsChanged;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs) => await _viewModel.InitializeAsync();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.TerminalOutput))
            ScrollTerminalToEnd();

        if (eventArgs.PropertyName == nameof(MainWindowViewModel.CurrentRomRow) &&
            _viewModel.FollowProgramCounter && _viewModel.CurrentRomRow is { } row)
            Dispatcher.UIThread.Post(() =>
            {
                RomList.ScrollIntoView(row);
                var source = _viewModel.AssemblySourceLines.FirstOrDefault(line => line.Addresses.Contains(row.AddressValue));
                if (source is not null) AssemblySourceList.ScrollIntoView(source);
                if (_viewModel.SelectedSourceInstructions.Contains(row))
                    SelectedSourceInstructionList.ScrollIntoView(row);
            }, DispatcherPriority.Background);
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
        => ScrollDiagnosticsToEnd();

    private void ScrollDiagnosticsToEnd()
    {
        DispatcherTimer.RunOnce(() =>
        {
            if (DiagnosticsList is { ItemCount: > 0 } list)
                list.ScrollIntoView(list.ItemCount - 1);
        }, TimeSpan.FromMilliseconds(20), DispatcherPriority.Background);
    }

    private void ScrollTerminalToEnd()
    {
        if (_terminalScrollPending) return;

        _terminalScrollPending = true;
        DispatcherTimer.RunOnce(() =>
        {
            _terminalScrollPending = false;
            TerminalScrollViewer.ScrollToEnd();
        }, TimeSpan.FromMilliseconds(20), DispatcherPriority.Background);
    }

    private void OnBottomTabChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is not TabControl tabs) return;
        if (tabs.SelectedIndex == 0) ScrollTerminalToEnd();
        if (tabs.SelectedIndex == 1) ScrollDiagnosticsToEnd();
    }

    private void OnMainTabChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Source, sender) || sender is not TabControl tabs) return;
        if (tabs.SelectedIndex != _currentMainTabIndex)
        {
            if (!_isNavigatingBack) _mainTabHistory.Push(_currentMainTabIndex);
            _currentMainTabIndex = tabs.SelectedIndex;
            _isNavigatingBack = false;
        }
        if (tabs.SelectedIndex != 1 || !_viewModel.FollowProgramCounter || _viewModel.CurrentRomRow is not { } current) return;

        var source = _viewModel.AssemblySourceLines.FirstOrDefault(line => line.Addresses.Contains(current.AddressValue));
        if (source is not null)
            Dispatcher.UIThread.Post(() => AssemblySourceList.ScrollIntoView(source), DispatcherPriority.Background);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsXButton1Pressed || !_mainTabHistory.TryPop(out var previous)) return;
        _isNavigatingBack = true;
        MainTabs.SelectedIndex = previous;
        eventArgs.Handled = true;
    }

    private void OnCollapsedBottomTabClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var index)) return;

        BottomTabs.SelectedIndex = index;
        _viewModel.IsBottomPanelExpanded = true;
        if (index == 0) ScrollTerminalToEnd();
        if (index == 1) ScrollDiagnosticsToEnd();
    }

    private void OnCpuSplitterPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pointerX = eventArgs.GetPosition(this).X;
        if (_viewModel.IsCpuPanelExpanded)
        {
            if (CpuPanelHost.Bounds.Width > _viewModel.CpuPanelMinWidth + 1)
            {
                _cpuMinimumPointerX = null;
                return;
            }

            _cpuMinimumPointerX ??= pointerX;
            if (pointerX <= _cpuMinimumPointerX - CollapseDragDistance)
            {
                _viewModel.IsCpuPanelExpanded = false;
                _cpuCollapsedPointerX = pointerX;
                eventArgs.Handled = true;
            }
        }
        else
        {
            eventArgs.Handled = true;
            if (_cpuCollapsedPointerX is { } collapsedAt && pointerX >= collapsedAt + CollapseDragDistance)
                _viewModel.IsCpuPanelExpanded = true;
        }
    }

    private void OnBottomSplitterPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pointerY = eventArgs.GetPosition(this).Y;
        if (_viewModel.IsBottomPanelExpanded)
        {
            if (BottomPanelHost.Bounds.Height > _viewModel.BottomPanelMinHeight + 1)
            {
                _bottomMinimumPointerY = null;
                return;
            }

            _bottomMinimumPointerY ??= pointerY;
            if (pointerY >= _bottomMinimumPointerY + CollapseDragDistance)
            {
                _viewModel.IsBottomPanelExpanded = false;
                _bottomCollapsedPointerY = pointerY;
                eventArgs.Handled = true;
            }
        }
        else
        {
            eventArgs.Handled = true;
            if (_bottomCollapsedPointerY is { } collapsedAt && pointerY <= collapsedAt - CollapseDragDistance)
                _viewModel.IsBottomPanelExpanded = true;
        }
    }

    private void OnPanelSplitterPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        _cpuMinimumPointerX = null;
        _cpuCollapsedPointerX = null;
        _bottomMinimumPointerY = null;
        _bottomCollapsedPointerY = null;
    }

    private async void OnShowAbout(object? sender, RoutedEventArgs eventArgs) =>
        await new AboutWindow().ShowDialog(this);

    private void OnExit(object? sender, RoutedEventArgs eventArgs) => Close();

    private async void OnGotoAddress(object? sender, RoutedEventArgs eventArgs) => await ShowGotoAddressAsync();

    private async Task ShowGotoAddressAsync()
    {
        var dialog = new GotoAddressWindow(_viewModel.ResolveNavigableAddress);
        var address = await dialog.ShowDialog<uint?>(this);
        if (address is null) return;

        var tab = _viewModel.SelectAddress(address.Value);
        MainTabs.SelectedIndex = tab;
        if (tab == 0 && _viewModel.SelectedRomRow is { } rom) RomList.ScrollIntoView(rom);
        if (tab == 2 && _viewModel.SelectedRamRow is { } ram) RamList.ScrollIntoView(ram);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.G && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            eventArgs.Handled = true;
            await ShowGotoAddressAsync();
            return;
        }

        var command = eventArgs.Key switch
        {
            Key.F8 => _viewModel.StepCycleCommand,
            Key.F9 => _viewModel.StepCommand,
            Key.F10 => _viewModel.SourceStepCommand,
            _ => null
        };
        if (command?.CanExecute(null) != true) return;
        eventArgs.Handled = true;
        await command.ExecuteAsync(null);
    }

    private void OnAssemblySourcePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var source = eventArgs.Source as Control;
        if (source is Button || source?.FindAncestorOfType<Button>() is not null) return;
        var item = source?.FindAncestorOfType<ListBoxItem>();
        if (item?.DataContext is not SourceCodeLineViewModel line ||
            !ReferenceEquals(line, _viewModel.SelectedAssemblySourceLine)) return;

        _viewModel.CloseSourceInstructions();
        eventArgs.Handled = true;
    }

    private void OnCloseSourceInstructions(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.CloseSourceInstructions();

    private void OnRomDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (_viewModel.SelectedRomRow is not { } rom) return;
        var source = _viewModel.AssemblySourceLines.FirstOrDefault(row => row.Addresses.Contains(rom.AddressValue));
        if (source is null) return;
        MainTabs.SelectedIndex = 1;
        AssemblySourceList.SelectedItem = source;
        AssemblySourceList.ScrollIntoView(source);
    }

    private void OnBreakpointDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (_viewModel.SelectedBreakpoint is { } breakpoint)
            NavigateToAddress(breakpoint.AddressValue);
    }

    private void OnSymbolDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (sender is ListBox { SelectedItem: SymbolRowViewModel symbol })
            NavigateToAddress(symbol.RawAddress);
    }

    private void NavigateToAddress(uint address)
    {
        var tab = _viewModel.SelectAddress(address);
        MainTabs.SelectedIndex = tab;
        if (tab == 0 && _viewModel.SelectedRomRow is { } rom) RomList.ScrollIntoView(rom);
        if (tab == 2 && _viewModel.SelectedRamRow is { } ram) RamList.ScrollIntoView(ram);
    }

    private async void OnAddWatch(object? sender, RoutedEventArgs eventArgs)
    {
        var dialog = new GotoAddressWindow(_viewModel.ResolveWatchAddress,
            "Add watch", "RAM or MMIO address, or symbol", "Add watch");
        var address = await dialog.ShowDialog<uint?>(this);
        if (address is not null) _viewModel.AddWatch(address.Value);
    }

    private async void OnAddBreakpoint(object? sender, RoutedEventArgs eventArgs)
    {
        var dialog = new GotoAddressWindow(_viewModel.ResolveBreakpointAddress,
            "Add breakpoint", "ROM address or symbol", "Add breakpoint");
        var address = await dialog.ShowDialog<uint?>(this);
        if (address is not null) _viewModel.AddBreakpoint(address.Value);
    }

    private void OnToggleSelectedRomBreakpoint(object? sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.SelectedRomRow is null) return;
        _viewModel.ToggleBreakpointCommand.Execute(null);
    }

    private void OnToggleRomBreakpoint(object? sender, RoutedEventArgs eventArgs)
    {
        if ((sender as Control)?.DataContext is RomRowViewModel row)
            _viewModel.ToggleRomBreakpoint(row);
    }

    private void OnToggleAssemblySourceBreakpoint(object? sender, RoutedEventArgs eventArgs)
    {
        var source = (sender as Control)?.DataContext as SourceCodeLineViewModel
            ?? AssemblySourceList.SelectedItem as SourceCodeLineViewModel;
        if (source is not null) _viewModel.ToggleAssemblySourceBreakpoint(source);
    }

    private void OnAddSourceWatches(object? sender, RoutedEventArgs eventArgs)
    {
        var source = (sender as Control)?.DataContext as SourceCodeLineViewModel
            ?? AssemblySourceList.SelectedItem as SourceCodeLineViewModel;
        if (source is not null) _viewModel.AddSourceWatches(source);
    }

    private void OnToggleSelectedRamWatch(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.ToggleSelectedRamWatch();

    private void OnOpenBuildACPU(object? sender, RoutedEventArgs eventArgs) =>
        BrowserLauncher.Open("https://buildacpu.com");

    private void OnTerminalTextInput(object? sender, TextInputEventArgs eventArgs)
    {
        if (string.IsNullOrEmpty(eventArgs.Text)) return;
        foreach (var character in eventArgs.Text)
            _viewModel.SendTerminalInput(character.ToString());
        eventArgs.Handled = true;
    }

    private void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        TerminalSurface.Focus();
        eventArgs.Handled = true;
    }

    private void OnTerminalGotFocus(object? sender, GotFocusEventArgs eventArgs)
    {
        TerminalFocusBadge.IsVisible = true;
        TerminalCursor.IsVisible = true;
    }

    private void OnTerminalLostFocus(object? sender, RoutedEventArgs eventArgs)
    {
        TerminalFocusBadge.IsVisible = false;
        TerminalCursor.IsVisible = false;
    }

    private void OnTerminalKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var text = eventArgs.Key switch
        {
            Key.Enter => "\n",
            Key.Back => "\b",
            _ => null
        };
        if (text is null) return;
        _viewModel.SendTerminalInput(text);
        eventArgs.Handled = true;
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Logs.CollectionChanged -= OnLogsChanged;
        _viewModel.Dispose();
    }
}
