using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace SCPU.Simulator.Desktop.Infrastructure;

public interface IDesktopFilePicker
{
    Task<string?> PickProgramAsync();
    Task<string?> PickExportPathAsync(string suggestedName, string displayName, string extension);
}

public sealed class DesktopFilePicker : IDesktopFilePicker
{
    public async Task<string?> PickProgramAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storage)
            return null;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open an S-CPU program",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("S-CPU programs")
                {
                    Patterns = ["*.bin", "*.rom", "*.asm", "*.s", "*.inc", "*.scode", "*.sc"]
                },
                FilePickerFileTypes.All
            ]
        });

        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }


    public async Task<string?> PickExportPathAsync(string suggestedName, string displayName, string extension)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storage)
            return null;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {displayName}",
            SuggestedFileName = suggestedName,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType(displayName) { Patterns = [$"*.{extension.TrimStart('.')}"] }
            ]
        });
        return file?.TryGetLocalPath();
    }
}
