using Microsoft.Azure.Cosmos;
using System.Text.Json;
using ProjectModels;

namespace CosmosAnalytics.ApiService.Data
{
    public class ProjectRepository
    {
        private readonly Container _container;

        public ProjectRepository(Container container)
        {
            _container = container;
        }

        public async Task<Project?> AddProjectAsync(Project project)
        {
            var response = await _container.CreateItemAsync(project, new PartitionKey(project.Id));
            return response.Resource;
        }

        public async Task<(List<JsonElement> Items, string? ContinuationToken)> GetRawProjectsAsync(int? pageSize, string? continuationToken)
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var results = new List<JsonElement>();
            var queryOptions = new QueryRequestOptions { MaxItemCount = pageSize ?? -1 };

            using var feed = _container.GetItemQueryIterator<JsonElement>(query, continuationToken, queryOptions);
            string? newContinuationToken = null;
            if (feed.HasMoreResults)
            {
                var response = await feed.ReadNextAsync();
                newContinuationToken = response.ContinuationToken;
                results.AddRange(response);
            }
            return (results, newContinuationToken);
        }

        public async Task<(List<Project> Items, string? ContinuationToken)> GetProjectsAsync(int? pageSize, string? continuationToken)
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var results = new List<Project>();
            var queryOptions = new QueryRequestOptions { MaxItemCount = pageSize ?? -1 };

            using var feed = _container.GetItemQueryIterator<Project>(query, continuationToken, queryOptions);
            string? newContinuationToken = null;
            if (feed.HasMoreResults)
            {
                var response = await feed.ReadNextAsync();
                newContinuationToken = response.ContinuationToken;
                results.AddRange(response);
            }
            return (results, newContinuationToken);
        }
    }
}
