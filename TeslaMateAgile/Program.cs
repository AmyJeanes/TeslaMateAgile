using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;
using TeslaMateAgile.Data.Enums;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Data.TeslaMate;
using TeslaMateAgile.Helpers;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Managers;
using TeslaMateAgile.Managers.Interfaces;
using TeslaMateAgile.Services;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile;

public class Program
{
    public async static Task Main()
    {
        var builder = new HostBuilder()
            .ConfigureAppConfiguration(configBuilder =>
            {
                configBuilder.AddJsonFile("appsettings.json");
                configBuilder.AddEnvironmentVariables();
                configBuilder.AddUserSecrets<Program>(true);
            })
            .ConfigureLogging((hostContext, loggingBuilder) =>
            {
                var config = hostContext.Configuration;
                loggingBuilder.AddConfiguration(config.GetSection("Logging"));
                loggingBuilder.AddConsole();
            })
            .ConfigureServices((hostContext, services) =>
            {
                var config = hostContext.Configuration;
                var connectionString = config.GetConnectionString("TeslaMate");
                if (string.IsNullOrEmpty(connectionString))
                {
                    string getDatabaseVariable(string variableName)
                    {
                        var option = config[variableName];
                        if (string.IsNullOrEmpty(option))
                        {
                            throw new ArgumentNullException(variableName, $"Configuration '{variableName}' or 'ConnectionStrings__TeslaMate' is required");
                        }
                        return option;
                    }
                    var databaseHost = getDatabaseVariable("DATABASE_HOST");
                    var databaseName = getDatabaseVariable("DATABASE_NAME");
                    var databaseUsername = getDatabaseVariable("DATABASE_USER");
                    var databasePassword = getDatabaseVariable("DATABASE_PASS");

                    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
                    {
                        Host = databaseHost,
                        Database = databaseName,
                        Username = databaseUsername,
                        Password = databasePassword
                    };

                    var databasePortVariable = "DATABASE_PORT";
                    var databasePortStr = config[databasePortVariable];
                    if (!string.IsNullOrEmpty(databasePortStr))
                    {
                        if (int.TryParse(databasePortStr, out var databasePort))
                        {
                            connectionStringBuilder.Port = databasePort;
                        }
                        else
                        {
                            throw new ArgumentException($"Configuration '{databasePortVariable}' is invalid, must be an integer", databasePortVariable);
                        }
                    }

                    connectionString = connectionStringBuilder.ConnectionString;
                }

                var builder = new DbConnectionStringBuilder();
                services.AddDbContext<TeslaMateDbContext>(o => o.UseNpgsql(connectionString));
                services.AddHostedService<PriceService>();
                services.AddOptions<TeslaMateOptions>()
                    .Bind(config.GetSection("TeslaMate"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();
                services.AddTransient<IPriceManager, PriceManager>();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<IRateLimitHelper, RateLimitHelper>();
                services.AddHttpClient();

                var energyProvider = config.GetValue("TeslaMate:EnergyProvider", EnergyProvider.Octopus);
                if (energyProvider == EnergyProvider.Octopus)
                {
                    services.AddOptions<OctopusOptions>()
                        .Bind(config.GetSection("Octopus"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, OctopusService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<OctopusOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else if (energyProvider == EnergyProvider.Tibber)
                {
                    services.AddOptions<TibberOptions>()
                        .Bind(config.GetSection("Tibber"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddTransient<IGraphQLJsonSerializer, SystemTextJsonSerializer>();
                    services.AddHttpClient<IPriceDataService, TibberService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<TibberOptions>>().Value;
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
                    });
                }
                else if (energyProvider == EnergyProvider.FixedPrice)
                {
                    services.AddOptions<FixedPriceOptions>()
                       .Bind(config.GetSection("FixedPrice"))
                       .ValidateDataAnnotations()
                       .ValidateOnStart();
                    services.AddSingleton<IPriceDataService, FixedPriceService>();
                }
                else if (energyProvider == EnergyProvider.FixedPriceWeekly)
                {
                    services.AddOptions<FixedPriceWeeklyOptions>()
                       .Bind(config.GetSection("FixedPriceWeekly"))
                       .ValidateDataAnnotations()
                       .ValidateOnStart();
                    services.AddSingleton<IPriceDataService, FixedPriceWeeklyService>();
                }
                else if (energyProvider == EnergyProvider.Awattar)
                {
                    services.AddOptions<AwattarOptions>()
                        .Bind(config.GetSection("Awattar"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, AwattarService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<AwattarOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else if (energyProvider == EnergyProvider.Energinet)
                {
                    services.AddOptions<EnerginetOptions>()
                        .Bind(config.GetSection("Energinet"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, EnerginetService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<EnerginetOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else if (energyProvider == EnergyProvider.HomeAssistant)
                {
                    services.AddOptions<HomeAssistantOptions>()
                        .Bind(config.GetSection("HomeAssistant"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, HomeAssistantService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
                    });
                }
                else if (energyProvider == EnergyProvider.Monta)
                {
                    services.AddOptions<MontaOptions>()
                        .Bind(config.GetSection("Monta"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, MontaService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<MontaOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else if (energyProvider == EnergyProvider.EDFTempo)
                {
                    services.AddOptions<EDFTempoOptions>()
                        .Bind(config.GetSection("EDFTempo"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, EDFTempoService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<EDFTempoOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else if (energyProvider == EnergyProvider.PGE)
                {
                    services.AddOptions<PGEOptions>()
                        .Bind(config.GetSection("PGE"))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();
                    services.AddHttpClient<IPriceDataService, PGEService>((serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptions<PGEOptions>>().Value;
                        var baseUrl = options.BaseUrl;
                        if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                        client.BaseAddress = new Uri(baseUrl);
                    });
                }
                else
                {
                    throw new ArgumentException("Invalid energy provider set", nameof(energyProvider));
                }
            });

        await builder.RunConsoleAsync();
    }
}
