using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Managers.Interfaces;

namespace TeslaMateAgile;

public class PriceService : IHostedService, IDisposable
{
    private readonly ILogger _logger;
    private readonly TeslaMateOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private Timer _timer;

    public PriceService(ILogger<PriceService> logger, IOptions<TeslaMateOptions> options, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _logger.LogInformation("Starting TeslaMateAgile (version: {Version})", version);
        var legacyImageWarning = Environment.GetEnvironmentVariable("TESLAMATE_LEGACY_IMAGE_WARNING");
        if (bool.TryParse(legacyImageWarning, out var warningEnabled) && warningEnabled)
        {
            _logger.LogWarning("You are using an old docker image repository. See GitHub for more information: https://github.com/AmyJeanes/TeslaMateAgile#why-am-i-seeing-a-warning-about-using-a-legacy-docker-image");
        }

        if (_options.UpdateIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_options.UpdateIntervalSeconds), _options.UpdateIntervalSeconds, "Must be greater than 0");
        }
        _logger.LogInformation("Price service is starting");
        _logger.LogInformation("Using energy provider {EnergyProvider}", _options.EnergyProvider);

        _timer = new Timer(async (state) =>
        {
            try
            {
                await DoWork();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run {nameof(DoWork)}");
            }
            _logger.LogInformation("Waiting {UpdateIntervalSeconds} seconds until next update", _options.UpdateIntervalSeconds);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.UpdateIntervalSeconds));

        return Task.CompletedTask;
    }

    private async Task DoWork()
    {
        _logger.LogDebug("Updating prices");

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var priceManager = scope.ServiceProvider.GetRequiredService<IPriceManager>();
                await priceManager.Update();
            }
            _logger.LogDebug("Price update complete");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update prices");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Price service is stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
