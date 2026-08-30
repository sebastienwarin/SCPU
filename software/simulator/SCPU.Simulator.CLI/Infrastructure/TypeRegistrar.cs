using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Infrastructure
{
    internal class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
    {
        private readonly IServiceCollection _services = services;
        private ServiceProvider? _provider;

        public ITypeResolver Build()
        {
            _provider ??= _services.BuildServiceProvider();   // build once
            return new TypeResolver(_provider);               // reuse
        }

        public void Register(Type service, Type implementation)
        => _services.AddSingleton(service, implementation);

        public void RegisterInstance(Type service, object implementation)
            => _services.AddSingleton(service, implementation);

        public void RegisterLazy(Type service, Func<object> factory)
            => _services.AddSingleton(service, sp => factory());
    }
}
