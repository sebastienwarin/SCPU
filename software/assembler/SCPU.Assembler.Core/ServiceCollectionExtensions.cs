using Microsoft.Extensions.DependencyInjection;
using SCPU.Assembler.Exporters;

namespace SCPU.Assembler
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register all assembler-related services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the S-CPU assembler core, parser, and all available exporters into the DI container.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <remarks>
        /// This is the main entry point for adding assembler functionality to a host application.
        /// It wires up:
        /// <list type="bullet">
        ///     <item><description><see cref="Parser"/>: parses assembly source into intermediate lines.</description></item>
        ///     <item><description><see cref="Assembler"/>: compiles parsed lines into machine code (<see cref="AssemblyResult"/>).</description></item>
        ///     <item><description><see cref="AssemblyExportManager"/>: central manager for exporting results to different formats.</description></item>
        ///     <item><description>All built-in <see cref="IAssemblyExporter"/> implementations (Annotated, Binary, IntelHex, Logisim16, Verilog, Gowin, Symbol).</description></item>
        /// </list>
        /// </remarks>
        public static void AddAssembler(this IServiceCollection services)
        {
            // Core services
            services.AddSingleton<Parser>();
            services.AddSingleton<Assembler>();

            // Exporters
            services.AddSingleton<AssemblyExportManager>();
            services.AddSingleton<IAssemblyExporter, AnnotatedExporter>();
            services.AddSingleton<IAssemblyExporter, BinaryExporter>();
            services.AddSingleton<IAssemblyExporter, IntelHexExporter>();
            services.AddSingleton<IAssemblyExporter, Logisim16Exporter>();
            services.AddSingleton<IAssemblyExporter, VerilogExporter>();
            services.AddSingleton<IAssemblyExporter, GowinExporter>();
            services.AddSingleton<IAssemblyExporter, SymbolExporter>();
        }
    }
}
