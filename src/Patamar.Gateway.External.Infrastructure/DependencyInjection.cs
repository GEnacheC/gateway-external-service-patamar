using Patamar.Gateway.External.Application.Abstractions;
using Patamar.Gateway.External.Application.Configuration;
using Patamar.Gateway.External.Application.Services;
using Patamar.Gateway.External.Infrastructure.Configuration;
using Patamar.Gateway.External.Infrastructure.CuritibaPmc;
using Patamar.Gateway.External.Infrastructure.Geocoding;
using Patamar.Gateway.External.Infrastructure.Persistence;
using Patamar.Gateway.External.Infrastructure.Sympla;
using Patamar.Gateway.External.Infrastructure.Ticketmaster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Patamar.Gateway.External.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SymplaOptions>(configuration.GetSection(SymplaOptions.SectionName));
        services.Configure<SymplaPublicOptions>(configuration.GetSection(SymplaPublicOptions.SectionName));
        services.Configure<CuritibaPmcOptions>(configuration.GetSection(CuritibaPmcOptions.SectionName));
        services.Configure<TicketmasterOptions>(configuration.GetSection(TicketmasterOptions.SectionName));
        services.Configure<GeocodingOptions>(configuration.GetSection(GeocodingOptions.SectionName));
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<RadarOptions>(configuration.GetSection(RadarOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>()
                ?? new MongoOptions();
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IEventRepository, MongoEventRepository>();
        services.AddSingleton<IRadarAreaRepository, MongoRadarAreaRepository>();
        services.AddSingleton<IGeocodingService, NominatimGeocodingService>();

        var geocoding = configuration.GetSection(GeocodingOptions.SectionName).Get<GeocodingOptions>()
            ?? new GeocodingOptions();
        services.AddHttpClient("nominatim", client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(geocoding.NominatimBaseUrl));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(geocoding.UserAgent);
        });

        services.AddHttpClient<SymplaService>((_, client) =>
        {
            var options = configuration.GetSection(SymplaOptions.SectionName).Get<SymplaOptions>()
                ?? new SymplaOptions();

            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                client.DefaultRequestHeaders.Remove("s_token");
                client.DefaultRequestHeaders.Add("s_token", options.Token);
            }
        });

        services.AddHttpClient<SymplaPublicSearchService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PatamarGatewayExternal/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHttpClient<CuritibaPmcService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PatamarGatewayExternal/1.0");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddHttpClient<TicketmasterService>((_, client) =>
        {
            var options = configuration.GetSection(TicketmasterOptions.SectionName).Get<TicketmasterOptions>()
                ?? new TicketmasterOptions();
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
        });

        services.AddTransient<IExternalService>(sp => sp.GetRequiredService<SymplaService>());
        services.AddTransient<IExternalService>(sp => sp.GetRequiredService<SymplaPublicSearchService>());
        services.AddTransient<IExternalService>(sp => sp.GetRequiredService<CuritibaPmcService>());
        services.AddTransient<IExternalService>(sp => sp.GetRequiredService<TicketmasterService>());

        services.AddScoped<IExternalServiceFactory, ExternalServiceFactory>();
        services.AddScoped<EventService>();

        return services;
    }

    private static string EnsureTrailingSlash(string baseUrl)
        => baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
}
