using Patamar.Gateway.External.Domain.Events;

namespace Patamar.Gateway.External.Application.Abstractions;

public interface IExternalService
{
    string ProviderName { get; }

    Task<IReadOnlyList<EventsDto>> GetEventsAsync(GetEventsQuery query, CancellationToken cancellationToken = default);
}
