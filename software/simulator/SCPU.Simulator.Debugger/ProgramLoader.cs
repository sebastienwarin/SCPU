using Microsoft.Extensions.Logging;
using SCode.Compiler;
using SCode.Compiler.Ast.Statements;
using SCPU.Architecture;
using SCPU.Assembler;
using SCPU.Assembler.Model;

namespace SCPU.Simulator.Debugger;

/// <summary>Loads ROM files and invokes the S-CPU assembler or S-Code compiler when needed.</summary>
public sealed class ProgramLoader(Assembler.Assembler assembler, Compiler compiler, ILogger<ProgramLoader> logger)
{
    /// <summary>Builds a program image from a binary, assembly or S-Code file.</summary>
    public async Task<ProgramImage> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists)
            throw new FileNotFoundException($"Program file not found: {file.FullName}", file.FullName);

        AssemblyResult? assembly = null;
        string? originalSource = null;
        string? generatedAssembly = null;
        string? generatedAssemblyIdentifier = null;
        ProgramFileType type;
        byte[] binary;

        if (IncludeStatement.SCodeFileExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
        {
            type = ProgramFileType.SCode;
            originalSource = await File.ReadAllTextAsync(file.FullName, cancellationToken);
            var compilation = await compiler.CompileAsync(new CompileRequest { Source = SourceDocument.FromFile(file) });
            generatedAssembly = await compilation.GeneratedAssembly.ReadAllTextAsync();
            generatedAssemblyIdentifier = compilation.GeneratedAssembly.Identifier;
            assembly = await assembler.AssembleAsync(new AssemblyRequest { Source = compilation.GeneratedAssembly });
            binary = assembly.Binary;
        }
        else if (IncludeStatement.AsmFileExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
        {
            type = ProgramFileType.Assembly;
            originalSource = await File.ReadAllTextAsync(file.FullName, cancellationToken);
            assembly = await assembler.AssembleAsync(new AssemblyRequest { Source = SourceDocument.FromFile(file) });
            binary = assembly.Binary;
        }
        else
        {
            type = ProgramFileType.Binary;
            binary = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
        }

        var words = ToWords(binary);
        var labelsByAddress = assembly?.Labels
            .GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(pair => pair.Key))) ?? [];
        var rom = new List<RomEntry>(words.Length);
        for (ushort address = 0; address < words.Length; address++)
        {
            object? source = assembly is not null && address < assembly.FinalWords.Count
                ? assembly.FinalWords[address].Source
                : null;
            labelsByAddress.TryGetValue(address, out var label);
            var isData = InstructionFormatter.IsDataWord(source);
            var instructionText = isData
                ? InstructionFormatter.FormatDataInstruction(words[address])
                : InstructionFormatter.Format(words[address]);
            rom.Add(new RomEntry(address, words[address], instructionText, label ?? "", source is null ? null : InstructionFormatter.GetSource(source))
            {
                IsData = isData
            });
        }

        var symbols = MergeSymbols(assembly);
        var sourceDocuments = await LoadSourceDocumentsAsync(rom,
            type == ProgramFileType.SCode ? generatedAssemblyIdentifier : file.FullName,
            type == ProgramFileType.SCode ? generatedAssembly : originalSource,
            cancellationToken);

        logger.LogInformation("Loaded {FileType} {File} ({Words} words).", type, file.FullName, words.Length);
        return new ProgramImage(file, type, binary, rom,
            symbols,
            InstructionUtils.DetectHaltAddresses(words))
        {
            AssemblyArtifact = assembly,
            OriginalSourceText = originalSource,
            GeneratedAssemblyText = generatedAssembly,
            GeneratedAssemblyIdentifier = generatedAssemblyIdentifier,
            SourceDocuments = sourceDocuments
        };
    }

    private static IReadOnlyDictionary<string, uint> MergeSymbols(AssemblyResult? assembly)
    {
        var symbols = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        if (assembly is null) return symbols;
        foreach (var constant in assembly.Constants)
            symbols[constant.Key] = unchecked((uint)constant.Value);
        foreach (var label in assembly.Labels)
            symbols[label.Key] = label.Value;
        return symbols;
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadSourceDocumentsAsync(
        IEnumerable<RomEntry> rom, string? primaryIdentifier, string? primaryText,
        CancellationToken cancellationToken)
    {
        var documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(primaryIdentifier) && primaryText is not null)
            documents[primaryIdentifier] = primaryText;

        foreach (var identifier in rom.Where(entry => entry.Source is not null)
                     .Select(entry => entry.Source!.Identifier).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (documents.ContainsKey(identifier) || !File.Exists(identifier)) continue;
            documents[identifier] = await File.ReadAllTextAsync(identifier, cancellationToken);
        }
        return documents;
    }

    private static ushort[] ToWords(byte[] bytes)
    {
        var words = new ushort[(bytes.Length + 1) / 2];
        for (var i = 0; i < words.Length; i++)
        {
            var high = bytes[i * 2];
            var low = i * 2 + 1 < bytes.Length ? bytes[i * 2 + 1] : 0;
            words[i] = (ushort)((high << 8) | low);
        }
        return words;
    }
}
