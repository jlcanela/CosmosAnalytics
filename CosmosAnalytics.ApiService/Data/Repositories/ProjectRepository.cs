using Microsoft.Azure.Cosmos;
using System.Text.Json;
using ProjectModels;
using Task = System.Threading.Tasks.Task;
using System.Text.Json.Serialization;
using EntitySearchApi.Models;
using Newtonsoft.Json.Linq;
using Jolt.Net;
using System.Diagnostics;


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
        private readonly JToken indexSpec = JToken.Parse(@"
        [
            {
                ""operation"": ""shift"",
                ""spec"": {
                    ""id"": [""projectid"", ""itemid""],
                    ""name"": ""index.name"",
                    ""description"": ""index.description"",
                    ""status"": ""index.status""                   
                }
            },
            {
                ""operation"": ""default"",
                ""spec"": {
                    ""id"": ""${id}"",
                    ""type"": ""project-index""
                }
            }
        ]");

        private readonly Chainr _chainr;

        private readonly Container _projectsContainer;
        private readonly ILogger<ProjectRepository> _logger;

        public ProjectRepository(CosmosContainers containers, ILogger<ProjectRepository> logger)
        {
            _projectsContainer = containers.Project();
            _logger = logger;
            _chainr = Chainr.FromSpec(indexSpec);

        }

        public static string PascalToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0]))
                return s;
            if (s.Length == 1)
                return s.ToLowerInvariant();
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        public string CreateIndexDocument(Project project)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());

            var json = JToken.Parse(JsonSerializer.Serialize(project, options));
            return _chainr.Transform(json).ToString(Newtonsoft.Json.Formatting.None); ;
        }

        public async Task<int> AddProjectsBulkAsync(IEnumerable<Project> projects)
        {
            var tasks = new List<Task<ItemResponse<Project>>>();
            var indexes = new List<Task<ItemResponse<JsonElement>>>();

            foreach (var project in projects)
            {
                project.ProjectId = project.Id;
                project.Type = "project";
                tasks.Add(_projectsContainer.CreateItemAsync(project, new PartitionKey(project.Id)));
                string guidString = Guid.NewGuid().ToString();
                var json = CreateIndexDocument(project).Replace("${id}", guidString);
                JsonDocument indexDocument = JsonDocument.Parse(json);
                indexes.Add(_projectsContainer.CreateItemAsync(indexDocument.RootElement));
            }

            var results = await Task.WhenAll(tasks);
            var resultsIndexes = await Task.WhenAll(indexes);
            foreach (var r in resultsIndexes)
            {
                Activity.Current?.SetTag("audit", r.ToString());
            }

            var createdProjects = new List<Project>();
            foreach (var result in results)
            {
                if (result.StatusCode == System.Net.HttpStatusCode.Created)
                    createdProjects.Add(result.Resource);
                // Optionally handle other status codes or exceptions
            }

            return createdProjects.Count;
        }

        public async Task<Project?> AddProjectAsync(Project project)
        {
            var response = await _projectsContainer.CreateItemAsync(project, new PartitionKey(project.Id));
            return response.Resource;
        }

        public async Task<(List<JsonElement> Items, string? ContinuationToken)> GetRawProjectsAsync(int? pageSize, string? continuationToken)
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var results = new List<JsonElement>();
            var queryOptions = new QueryRequestOptions { MaxItemCount = pageSize ?? -1 };

            using var feed = _projectsContainer.GetItemQueryIterator<JsonElement>(query, continuationToken, queryOptions);
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

            using var feed = _projectsContainer.GetItemQueryIterator<Project>(query, continuationToken, queryOptions);
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
            var sql = "SELECT c.itemid FROM c";
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
                                    filters.Add($"c.index.{field} = {paramName}");
                                    parameters[paramName] = ssp.Value;
                                    break;
                                case StringOperation.Contains:
                                    filters.Add($"CONTAINS(c.index.{field}, {paramName})");
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
                                            orClauses.Add($"c.index.{field} = {enumParam}");
                                            parameters[enumParam] = esp.Value[j];
                                        }
                                        filters.Add($"({string.Join(" OR ", orClauses)})");
                                    }
                                    else
                                    {
                                        filters.Add($"c.index.{field} = {paramName}");
                                        parameters[paramName] = esp.Value[0];
                                    }
                                    break;
                                case EnumOperation.Contains:
                                    filters.Add($"ARRAY_CONTAINS({paramName}, c.index.{field})");
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
                                    filters.Add($"c.index.{field} < {paramName}");
                                    parameters[paramName] = dsp.Value;
                                    break;
                                case DateOperation.After:
                                    filters.Add($"c.index.{field} > {paramName}");
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

            filters.Add("c.type = \"project-index\"");
            sql += " WHERE " + string.Join(" AND ", filters);

            // Sorting
            if (searchRequest.Sort != null && searchRequest.Sort.Count > 0)
            {
                var orderClauses = searchRequest.Sort
                    .Select(sf => $"c.index.{sf.Field} {(sf.Order?.ToString().ToUpper() ?? "ASC")}");
                sql += " ORDER BY " + string.Join(", ", orderClauses);
            }
            else
            {
                sql += " ORDER BY c.index.name ASC"; // Default sort
            }

            var queryDef = new QueryDefinition(sql);
            foreach (var param in parameters)
            {
                queryDef = queryDef.WithParameter(param.Key, param.Value);
            }

            var queryOptions = new QueryRequestOptions { MaxItemCount = searchRequest.PageSize ?? -1 };

            string? newContinuationToken = null;
            var itemIds = new List<string>();
            using (var indexFeed = _projectsContainer.GetItemQueryIterator<dynamic>(queryDef, searchRequest.ContinuationToken, queryOptions))
            {
                if (indexFeed.HasMoreResults)
                {
                    var response = await indexFeed.ReadNextAsync();
                    newContinuationToken = response.ContinuationToken;
                    foreach (var doc in response)
                    {
                        // Cast to JsonElement
                        JsonElement element = (JsonElement)doc;

                        if (element.TryGetProperty("itemid", out JsonElement itemIdElement))
                        {
                            string itemId = itemIdElement.GetString();
                            itemIds.Add(itemId);
                        }
                        else
                        {
                            _logger.LogWarning("itemid property not found in document.");
                        }
                    }
                }
            }

            if (itemIds.Count == 0)
                return (new List<Project>(), null);

            var inClause = string.Join(", ", itemIds.Select(id => $"'{id}'"));
            var projectSql = $"SELECT * FROM c WHERE c.id IN ({inClause}) AND c.type = 'project'";

            var projectQueryDef = new QueryDefinition(projectSql);

            var results = new List<Project>();
            using (var projectFeed = _projectsContainer.GetItemQueryIterator<Project>(projectQueryDef))
            {
                if (projectFeed.HasMoreResults)
                {
                    var response = await projectFeed.ReadNextAsync();
                    results.AddRange(response);
                    //return (results, newContinuationToken);
                }
            }

            // After fetching 'results' from the projects container
            var resultsDict = results.ToDictionary(p => p.Id, p => p);

            // Order results according to itemIds
            var orderedResults = new List<Project>();
            foreach (var id in itemIds)
            {
                if (resultsDict.TryGetValue(id, out var project))
                {
                    orderedResults.Add(project);
                }
            }
            return (orderedResults, newContinuationToken);

        }

    }
}
