using System.Text.Json;
using System.Text.Json.Serialization;
using CosmosAnalytics.ApiService;
using FakeData;
using Microsoft.Azure.Cosmos;
using ProjectModels;

namespace ApiServices;

public class ProjectService
{
    private readonly Container _container;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(Container container, ILogger<ProjectService> logger)
    {
        _container = container;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public Project GenerateSampleProject()
    {
        var project = new CustomProjectFaker().Generate();
        _logger.LogInformation("Generated project: {Id}", project.Id);
        _logger.LogInformation(JsonSerializer.Serialize(project, _jsonOptions));
        return project;
    }

    public async Task<Project> AddProjectAsync(Project project)
    {
        try
        {
            var response = await _container.CreateItemAsync(project, new PartitionKey(project.Id));
            _logger.LogInformation("Inserted project: {Id}", project.Id);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to insert project");
            throw;
        }
    }

    public async Task<PaginatedResponse<JsonElement>> GetRawProjectsAsync(
        int? pageSize, string? continuationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var results = new List<JsonElement>();
        string? newContinuationToken = null;

        var queryOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize ?? -1
        };

        using var feed = _container.GetItemQueryIterator<JsonElement>(
            query,
            continuationToken: continuationToken,
            requestOptions: queryOptions);

        if (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            newContinuationToken = response.ContinuationToken;
            results.AddRange(response);
        }

        return new PaginatedResponse<JsonElement>(results, newContinuationToken, results.Count);
    }

    public async Task<PaginatedResponse<Project>> GetProjectsAsync(
        int? pageSize, string? continuationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var results = new List<Project>();
        string? newContinuationToken = null;

        var queryOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize ?? -1
        };

        using var feed = _container.GetItemQueryIterator<Project>(
            query,
            continuationToken: continuationToken,
            requestOptions: queryOptions);

        if (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            newContinuationToken = response.ContinuationToken;
            results.AddRange(response);
        }

        return new PaginatedResponse<Project>(results, newContinuationToken, results.Count);
    }
}
