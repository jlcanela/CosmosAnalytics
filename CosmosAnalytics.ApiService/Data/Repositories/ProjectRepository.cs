using Microsoft.Azure.Cosmos;
using System.Text.Json;
using ProjectModels;
using Task = System.Threading.Tasks.Task;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using EntitySearchApi.Models;
using Bogus.DataSets;

namespace CosmosAnalytics.ApiService.Data
{

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(ProjectSearchRequest), "search-project")]
    public abstract class SearchRequest
    {
        // You can add common properties here if needed
        public string? ContinuationToken { get; set; }
        public int? PageSize { get; set; }

    }

    public class ProjectSearchRequest : SearchRequest
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public List<string>? Tags { get; set; }
        public string? Owner { get; set; }

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

        public async Task<(List<Project> Items, string? ContinuationToken)> SearchAsync(EntitySearchRequest searchRequest)
        {
            var sql = "SELECT * FROM c";
            var filters = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (searchRequest.SearchParameters != null)
            {
                for (int i = 0; i < searchRequest.SearchParameters.Count; i++)
                {
                    var sp = searchRequest.SearchParameters[i];
                    var paramName = $"@param{i}";
                    var field = sp.Field;

                    switch (sp)
                    {
                        case StringSearchParameter ssp:
                            switch (ssp.Operation)
                            {
                                case StringOperation.Equals:
                                    filters.Add($"c.{field} = {paramName}");
                                    parameters[paramName] = ssp.Value;
                                    break;
                                case StringOperation.Contains:
                                    filters.Add($"CONTAINS(c.{field}, {paramName})");
                                    parameters[paramName] = ssp.Value;
                                    break;
                                // Add more string operations as needed
                                default:
                                    throw new NotSupportedException($"String operation {ssp.Operation} not supported");
                            }
                            break;

                        case EnumSearchParameter esp:
                            switch (esp.Operation)
                            {
                                case EnumOperation.Equals:
                                    // If multiple values, generate OR clause
                                    if (esp.Value.Count > 1)
                                    {
                                        var orClauses = new List<string>();
                                        for (int j = 0; j < esp.Value.Count; j++)
                                        {
                                            var enumParam = $"{paramName}_{j}";
                                            orClauses.Add($"c.{field} = {enumParam}");
                                            parameters[enumParam] = esp.Value[j];
                                        }
                                        filters.Add($"({string.Join(" OR ", orClauses)})");
                                    }
                                    else
                                    {
                                        filters.Add($"c.{field} = {paramName}");
                                        parameters[paramName] = esp.Value[0];
                                    }
                                    break;
                                case EnumOperation.Contains:
                                    filters.Add($"ARRAY_CONTAINS({paramName}, c.{field})");
                                    parameters[paramName] = esp.Value;
                                    break;
                                default:
                                    throw new NotSupportedException($"Enum operation {esp.Operation} not supported");
                            }
                            break;

                        case DateSearchParameter dsp:
                            switch (dsp.Operation)
                            {
                                case DateOperation.Before:
                                    filters.Add($"c.{field} < {paramName}");
                                    parameters[paramName] = dsp.Value;
                                    break;
                                case DateOperation.After:
                                    filters.Add($"c.{field} > {paramName}");
                                    parameters[paramName] = dsp.Value;
                                    break;
                                default:
                                    throw new NotSupportedException($"Date operation {dsp.Operation} not supported");
                            }
                            break;

                        // Add handling for NumberSearchParameter, NumberRangeSearchParameter, DateRangeSearchParameter, UniversalSearchParameter as needed

                        default:
                            throw new NotSupportedException($"SearchParameter type {sp.GetType().Name} not supported");
                    }
                }
            }

            if (filters.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", filters);
            }

            // Sorting
            if (searchRequest.Sort != null && searchRequest.Sort.Count > 0)
            {
                var orderClauses = searchRequest.Sort
                    .Select(sf => $"c.{sf.Field} {(sf.Order?.ToString().ToUpper() ?? "ASC")}");
                sql += " ORDER BY " + string.Join(", ", orderClauses);
            }
            else
            {
                // sql += " ORDER BY c.name ASC"; // Default sort
            }

            _logger.LogInformation("SQL: " + sql);

            var queryDef = new QueryDefinition(sql);
            foreach (var param in parameters)
            {
                queryDef = queryDef.WithParameter(param.Key, param.Value);
            }

            var results = new List<Project>();
            var queryOptions = new QueryRequestOptions { MaxItemCount = searchRequest.PageSize ?? -1 };

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
