using System.Text.Json;

namespace SCPU.Simulator.Desktop.Infrastructure;

public sealed record DesktopSettings(bool CpuPanelExpanded = true, bool BottomPanelExpanded = true);

/// <summary>Persists small, resolution-independent desktop UI preferences.</summary>
public sealed class DesktopSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SCPU", "Simulator", "settings.json");

    public DesktopSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(_path)) ?? new DesktopSettings()
                : new DesktopSettings();
        }
        catch (JsonException) { return new DesktopSettings(); }
        catch (IOException) { return new DesktopSettings(); }
    }

    public void Save(DesktopSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
