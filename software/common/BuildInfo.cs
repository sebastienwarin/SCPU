using System.Reflection;

namespace SCPU;

/// <summary>
/// Exposes the user-facing version embedded in the entry assembly by MSBuild.
/// </summary>
internal static class BuildInfo
{
    public static string Version
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "development";
        }
    }
}
