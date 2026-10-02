namespace Patamar.Gateway.External.Application.Abstractions;

public interface IExternalServiceFactory
{
    IExternalService Create(string provider);

    IReadOnlyCollection<string> GetAvailableProviders();
}
