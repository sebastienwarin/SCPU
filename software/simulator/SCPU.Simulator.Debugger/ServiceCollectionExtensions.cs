using Microsoft.Extensions.DependencyInjection;
using SCode.Compiler;
using SCPU.Assembler;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Debugger;

/// <summary>Registers the shared S-CPU debugger services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the toolchain, processor, debug session and sequential runner.</summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="processorFactory">Optional frontend-owned machine composition.</param>
    public static IServiceCollection AddSCPUDebugger(
        this IServiceCollection services,
        Func<IServiceProvider, Processor>? processorFactory = null)
    {
        services.AddAssembler();
        services.AddCompiler();
        services.AddSingleton(processorFactory ?? (_ => new Processor()));
        services.AddSingleton<ProgramLoader>();
        services.AddSingleton<ProgramExporter>();
        services.AddSingleton<DebugSession>();
        services.AddSingleton<SimulationRunner>();
        return services;
    }
}
