using Microsoft.Azure.Cosmos;
using System.Text.Json;
using ProjectModels;
using Task = System.Threading.Tasks.Task;
using System.Net.Http.Headers;

namespace CosmosAnalytics.ApiService.Data
{

    public class ProjectSearchRequest
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public List<string>? Tags { get; set; }
        public string? Owner { get; set; }
        public int? PageSize { get; set; }
        public string? ContinuationToken { get; set; }
    }

    public class ProjectRepository
    {
        private readonly Container _container;
        private readonly ILogger<ProjectRepository> _logger;

        public ProjectRepository(Container container, ILogger<ProjectRepository> logger)
        {
            _container = container;
            _logger = logger;
        }

        public static string PascalToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0]))
                return s;
            if (s.Length == 1)
                return s.ToLowerInvariant();
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        public async Task<List<Project>> AddProjectsBulkAsync(IEnumerable<Project> projects)
        {
            var tasks = new List<Task<ItemResponse<Project>>>();

            foreach (var project in projects)
            {
                tasks.Add(_container.CreateItemAsync(project, new PartitionKey(project.Id)));
            }

            var results = await Task.WhenAll(tasks);

            // You can handle failures here if needed (results may contain exceptions)
            var createdProjects = new List<Project>();
            foreach (var result in results)
            {
                if (result.StatusCode == System.Net.HttpStatusCode.Created)
                    createdProjects.Add(result.Resource);
                // Optionally handle other status codes or exceptions
            }

            return createdProjects;
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

        public async Task<(List<Project> Items, string? ContinuationToken)> SearchProjectsAsync(ProjectSearchRequest searchRequest)
        {
            var sql = "SELECT * FROM c";
            var filters = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(searchRequest.Name))
            {
                filters.Add("CONTAINS(c.name, @name)");
                parameters.Add("@name", searchRequest.Name);
                _logger.LogInformation("Name filter:" + "\"" + searchRequest.Name);
            }
            if (!string.IsNullOrEmpty(searchRequest.Status))
            {
                filters.Add("c.status = @status");
                parameters.Add("@status", searchRequest.Status);
            }
            if (searchRequest.CreatedAfter.HasValue)
            {
                filters.Add("c.created >= @createdAfter");
                parameters.Add("@createdAfter", searchRequest.CreatedAfter.Value);
            }
            if (searchRequest.CreatedBefore.HasValue)
            {
                filters.Add("c.created <= @createdBefore");
                parameters.Add("@createdBefore", searchRequest.CreatedBefore.Value);
            }
            if (!string.IsNullOrEmpty(searchRequest.Owner))
            {
                filters.Add("c.owner = @owner");
                parameters.Add("@owner", searchRequest.Owner);
            }
            if (searchRequest.Tags != null && searchRequest.Tags.Count > 0)
            {
                filters.Add("ARRAY_CONTAINS(@tags, t) IN (SELECT VALUE t FROM t IN c.tags)");
                parameters.Add("@tags", searchRequest.Tags);
            }

            if (filters.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", filters);
            }

            sql += " ORDER BY c.name ASC";

            var queryDef = new QueryDefinition(sql);

            foreach (var param in parameters)
            {
                queryDef = queryDef.WithParameter(param.Key, param.Value);
                Console.WriteLine(param.Key + "=" + param.Value);
            }

            Console.WriteLine(queryDef.QueryText);

            var results = new List<Project>();
            var queryOptions = new QueryRequestOptions { MaxItemCount = searchRequest.PageSize ?? -1 };

            _logger.BeginScope("Project Search");

            using var feed = _container.GetItemQueryIterator<Project>(
                queryDef,
                searchRequest.ContinuationToken,
                queryOptions
            );

            string? newContinuationToken = null;
            if (feed.HasMoreResults)
            {
                var response = await feed.ReadNextAsync();
                newContinuationToken = response.ContinuationToken;
                results.AddRange(response);
            }

            _logger.LogInformation("Result Count:" + results.Count);


            return (results, newContinuationToken);
        }

    }
}
