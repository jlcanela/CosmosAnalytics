using Microsoft.Azure.Cosmos;

public class StartupOrHostedService : IHostedService
{
    private readonly CosmosIndexingPolicyService _indexingService;
    private readonly Container _container;
    private readonly ILogger<StartupOrHostedService> _logger;

    public StartupOrHostedService(
        CosmosIndexingPolicyService indexingService,
        Container container,
        ILogger<StartupOrHostedService> logger)
    {
        _indexingService = indexingService;
        _container = container;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running Cosmos indexing policy check at startup...");
        await _indexingService.EnsureCompositeIndexAsync(_container);
        _logger.LogInformation("Cosmos indexing policy check completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
