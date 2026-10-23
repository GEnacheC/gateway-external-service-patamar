using Patamar.Gateway.External.Application.Abstractions;

namespace Patamar.Gateway.External.Application.Services;

public sealed class ExternalServiceFactory(IEnumerable<IExternalService> services) : IExternalServiceFactory
{
    private readonly Dictionary<string, IExternalService> _services = services
        .ToDictionary(s => s.ProviderName, StringComparer.OrdinalIgnoreCase);

    public IExternalService Create(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (_services.TryGetValue(provider, out var service))
        {
            return service;
        }

        throw new ArgumentException(
            $"Unknown external provider '{provider}'. Available: {string.Join(", ", _services.Keys.OrderBy(x => x))}",
            nameof(provider));
    }

    public IReadOnlyCollection<string> GetAvailableProviders()
        => _services.Keys.OrderBy(x => x).ToArray();
}
