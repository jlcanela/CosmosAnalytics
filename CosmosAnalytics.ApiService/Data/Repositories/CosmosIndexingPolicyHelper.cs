using Microsoft.Azure.Cosmos;
using System.Collections.ObjectModel;

public class CosmosIndexingPolicyService
{
    private readonly ILogger<CosmosIndexingPolicyService> _logger;

    public CosmosIndexingPolicyService(ILogger<CosmosIndexingPolicyService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnsureCompositeIndexAsync(Container container)
    {
        var containerResponse = await container.ReadContainerAsync();
        var containerProperties = containerResponse.Resource;
        var indexingPolicy = containerProperties.IndexingPolicy;

        // Define the desired composite index
        var desiredComposite = new Collection<CompositePath>
        {
            new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/name", Order = CompositePathSortOrder.Ascending }
        };

        // Check if the composite index already exists
        bool exists = indexingPolicy.CompositeIndexes.Any(existing =>
            existing.Count == desiredComposite.Count &&
            existing.Zip(desiredComposite, (a, b) => a.Path == b.Path && a.Order == b.Order).All(x => x)
        );

        if (!exists)
        {
            _logger.LogInformation("Composite index missing, adding it.");
            indexingPolicy.CompositeIndexes.Add(desiredComposite);
            await container.ReplaceContainerAsync(containerProperties);
            _logger.LogInformation("Composite index added and container updated.");
            return true;
        }
        else
        {
            _logger.LogInformation("Composite index already exists, no update needed.");
            return false;
        }
    }
}
