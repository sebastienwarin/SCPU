using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCPU.Assembler.Exporters;
using SCPU.Simulator.Debugger;

namespace SCPU.Simulator.Debugger.Tests;

public sealed class ProgramLoaderTests
{
    [Fact]
    public async Task Binary_load_preserves_big_endian_words_and_odd_byte()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scpu-{Guid.NewGuid():N}.rom");
        await File.WriteAllBytesAsync(path, [0x12, 0x34, 0xAB]);
        try
        {
            using var services = CreateServices();
            var image = await services.GetRequiredService<ProgramLoader>().LoadAsync(path);
            Assert.Equal(ProgramFileType.Binary, image.Type);
            Assert.Equal((ushort)0x1234, image.Rom[0].Value);
            Assert.Equal((ushort)0xAB00, image.Rom[1].Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Assembly_load_retains_symbols_and_source_mapping()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scpu-{Guid.NewGuid():N}.asm");
        var exportPath = Path.Combine(Path.GetTempPath(), $"scpu-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "#const TTY = 0x12801\nstart: nor #0\n");
        try
        {
            using var services = CreateServices();
            var image = await services.GetRequiredService<ProgramLoader>().LoadAsync(path);
            Assert.Equal(ProgramFileType.Assembly, image.Type);
            var address = image.Symbols["start"];
            Assert.True(address > 0); // The assembler prepends the S-CPU bootloader.
            Assert.Equal(2, image.Rom[(int)address].Source?.Line);
            Assert.NotNull(image.AssemblyArtifact);
            Assert.Contains("start: nor #0", image.OriginalSourceText);
            Assert.Null(image.GeneratedAssemblyText);
            Assert.Equal(0x12801u, image.Symbols["TTY"]);

            await services.GetRequiredService<ProgramExporter>()
                .ExportAsync(image, new FileInfo(exportPath), OutputFormat.Annotated);
            var annotated = await File.ReadAllTextAsync(exportPath);
            Assert.Contains("start", annotated);
            Assert.Contains("nor #0", annotated, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
            File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task Assembly_load_retains_mapped_include_documents()
    {
        var directory = Directory.CreateTempSubdirectory("scpu-include-");
        var root = Path.Combine(directory.FullName, "program.asm");
        var include = Path.Combine(directory.FullName, "library.asm");
        await File.WriteAllTextAsync(root, "#include \"library.asm\"\nstart: nor #0\n");
        await File.WriteAllTextAsync(include, "included: add #1\n");
        try
        {
            using var services = CreateServices();
            var image = await services.GetRequiredService<ProgramLoader>().LoadAsync(root);

            Assert.Equal(await File.ReadAllTextAsync(root), image.SourceDocuments[root]);
            Assert.Equal(await File.ReadAllTextAsync(include), image.SourceDocuments[include]);
            Assert.Contains(image.Rom, entry => entry.Source?.Identifier == include);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddSCPUDebugger();
        return services.BuildServiceProvider();
    }
}
