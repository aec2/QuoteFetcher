using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace QuoteFetcher;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services;

    public TypeRegistrar(IServiceProvider provider)
    {
        _services = new ServiceCollection();

        // Add the IServiceProvider directly
        _services.AddSingleton(provider);
    }

    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _services.AddSingleton(service, _ => factory());
    }
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider) => _provider = provider;

    public object? Resolve(Type? type)
    {
        if (type == null) return null;

        // Try to resolve the type
        return _provider.GetService(type);
    }

    public void Dispose() => (_provider as IDisposable)?.Dispose();
}