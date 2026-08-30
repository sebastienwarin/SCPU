using SCPU.Assembler;
using SCPU.Assembler.Exporters;

namespace SCPU.Simulator.Debugger;

/// <summary>Exports the currently loaded program through the assembler output formats.</summary>
public sealed class ProgramExporter(AssemblyExportManager exportManager)
{
    /// <summary>Writes a program image in the requested output format.</summary>
    public Task ExportAsync(ProgramImage program, FileInfo output, OutputFormat format,
        CancellationToken cancellationToken = default)
    {
        // Preserve the original Line objects, macro expansions, constants and labels.
        // A raw binary has no assembly metadata, so only that case needs a minimal artifact.
        var result = program.AssemblyArtifact ?? new AssemblyResult
        {
            Binary = program.Binary,
            Labels = new Dictionary<string, uint>(program.Symbols),
            FinalWords = program.Rom.Select(entry => ((object)entry.Instruction, entry.Value)).ToList()
        };
        return exportManager.WriteAsync(result, output, format, cancellationToken);
    }
}
