using Microsoft.Extensions.DependencyInjection;

namespace SCode.Compiler
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register all compiler-related services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the S-Code compiler core, parser, and all available exporters into the DI container.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <remarks>
        /// This is the main entry point for adding compiler functionality to a host application.
        /// </remarks>
        public static void AddCompiler(this IServiceCollection services)
        {
            // Core services
            services.AddSingleton<Compiler>();
        }
    }
}
