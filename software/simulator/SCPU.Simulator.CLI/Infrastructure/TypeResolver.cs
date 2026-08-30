using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Infrastructure
{
    internal class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
    {
        private readonly ServiceProvider _provider = provider;

        public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);

        public void Dispose() { }
    }
}
