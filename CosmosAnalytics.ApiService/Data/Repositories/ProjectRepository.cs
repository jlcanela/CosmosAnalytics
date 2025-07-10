using Microsoft.Azure.Cosmos;
using System.Text.Json;
using ProjectModels;
using Task = System.Threading.Tasks.Task;
using System.Text.Json.Serialization;
using EntitySearchApi.Models;
using Newtonsoft.Json.Linq;
using Jolt.Net;
using System.Diagnostics;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;

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

    public class ProjectIndex
    {
        [JsonPropertyName("projectid")]
        public string ProjectId { get; set; }

        [JsonPropertyName("itemid")]
        public string ItemId { get; set; }

        [JsonPropertyName("index")]
        public ProjectIndexFields Index { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }


    }

    public class ProjectIndexFields
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("name_ft")]
        public List<string> NameFullText { get; set; }

        [JsonPropertyName("description_ft")]
        public List<string> DescriptionFullText { get; set; }
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
            },
            {
                ""operation"": ""ft"",
                ""spec"": {
                    ""fields"": [ ""name"", ""description""] 
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
            _chainr = Chainr.FromSpec(indexSpec, new Dictionary<string, Type>
            {
                { "ft", typeof(FtTransform) }
            });
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

        // [Partial Implementation] of SearchAsync using LINQ does not support partial match but only full matches
        // diacritic aware search is using an array representation of the string
        // according to https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/linq-to-sql 
        // p.Index.NameFullText.Contains(tokens[0])) map to ARRAY_CONTAINS
        // according to https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/array-contains
        // bool_expr of ARRAY_CONTAINS(<array_expr>, <expr> [, <bool_expr>]) is not accessible from LINQ Contains
        // => partial search is not possible as bool_expr default value "false" is a full match contains 
        // Additionnaly the typesafe approach with Linq totatly contradicts the dynamic approach of using the EntitySearchRequest model
        public async Task<(List<Project> Items, string? ContinuationToken)> SearchWithLinqAsync(EntitySearchRequest searchRequest)
        {
            IQueryable<ProjectIndex> query = _projectsContainer.GetItemLinqQueryable<ProjectIndex>(allowSynchronousQueryExecution: true);

            // Build filters (all direct property access, no predicate composition)
            if (searchRequest.SearchParameters != null)
            {
                foreach (var sp in searchRequest.SearchParameters)
                {
                    var field = sp.Field.ToLowerInvariant();

                    switch (sp)
                    {
                        case StringSearchParameter ssp:
                            switch (ssp.Operation)
                            {
                                case StringOperation.Equals:
                                    if (field == "name")
                                        query = query.Where(p => p.Index.Name == ssp.Value);
                                    else if (field == "description")
                                        query = query.Where(p => p.Index.Description == ssp.Value);
                                    else if (field == "status")
                                        query = query.Where(p => p.Index.Status == ssp.Value);
                                    else
                                        throw new NotSupportedException($"Field {field} is not supported for string search.");
                                    break;

                                case StringOperation.Contains:
                                    var tokens = LowercaseAsciiFoldingAnalyzer.AnalyzeText(ssp.Value?.ToString() ?? "");
                                    if (tokens.Length == 0)
                                        break;
                                    if (field == "name")
                                    {
                                        if (tokens.Length == 1)
                                            query = query.Where(p => p.Index.NameFullText.Contains(tokens[0]));
                                        else if (tokens.Length == 2)
                                            query = query.Where(p => p.Index.NameFullText.Contains(tokens[0]) || p.Index.NameFullText.Contains(tokens[1]));
                                        else if (tokens.Length == 3)
                                            query = query.Where(p => p.Index.NameFullText.Contains(tokens[0]) || p.Index.NameFullText.Contains(tokens[1]) || p.Index.NameFullText.Contains(tokens[2]));
                                        else
                                            throw new NotSupportedException("Too many tokens for 'Contains' search. Please limit to 3.");
                                    }
                                    else if (field == "description")
                                    {
                                        if (tokens.Length == 1)
                                            query = query.Where(p => p.Index.DescriptionFullText.Contains(tokens[0]));
                                        else if (tokens.Length == 2)
                                            query = query.Where(p => p.Index.DescriptionFullText.Contains(tokens[0]) || p.Index.DescriptionFullText.Contains(tokens[1]));
                                        else if (tokens.Length == 3)
                                            query = query.Where(p => p.Index.DescriptionFullText.Contains(tokens[0]) || p.Index.DescriptionFullText.Contains(tokens[1]) || p.Index.DescriptionFullText.Contains(tokens[2]));
                                        else
                                            throw new NotSupportedException("Too many tokens for 'Contains' search. Please limit to 3.");
                                    }
                                    else
                                        throw new NotSupportedException($"Field {field} is not supported for string contains search.");
                                    break;

                                default:
                                    throw new NotSupportedException($"String operation {ssp.Operation} not supported");
                            }
                            break;

                        case EnumSearchParameter esp:
                            switch (esp.Operation)
                            {
                                case EnumOperation.Equals:
                                    if (esp.Value.Count == 1)
                                    {
                                        var value = esp.Value[0];
                                        if (field == "name")
                                            query = query.Where(p => p.Index.Name == value);
                                        else if (field == "description")
                                            query = query.Where(p => p.Index.Description == value);
                                        else if (field == "status")
                                            query = query.Where(p => p.Index.Status == value);
                                        else
                                            throw new NotSupportedException($"Field {field} is not supported for enum equals search.");
                                    }
                                    else if (esp.Value.Count == 2)
                                    {
                                        var v1 = esp.Value[0];
                                        var v2 = esp.Value[1];
                                        if (field == "name")
                                            query = query.Where(p => p.Index.Name == v1 || p.Index.Name == v2);
                                        else if (field == "description")
                                            query = query.Where(p => p.Index.Description == v1 || p.Index.Description == v2);
                                        else if (field == "status")
                                            query = query.Where(p => p.Index.Status == v1 || p.Index.Status == v2);
                                        else
                                            throw new NotSupportedException($"Field {field} is not supported for enum equals search.");
                                    }
                                    else if (esp.Value.Count == 3)
                                    {
                                        var v1 = esp.Value[0];
                                        var v2 = esp.Value[1];
                                        var v3 = esp.Value[2];
                                        if (field == "name")
                                            query = query.Where(p => p.Index.Name == v1 || p.Index.Name == v2 || p.Index.Name == v3);
                                        else if (field == "description")
                                            query = query.Where(p => p.Index.Description == v1 || p.Index.Description == v2 || p.Index.Description == v3);
                                        else if (field == "status")
                                            query = query.Where(p => p.Index.Status == v1 || p.Index.Status == v2 || p.Index.Status == v3);
                                        else
                                            throw new NotSupportedException($"Field {field} is not supported for enum equals search.");
                                    }
                                    else
                                    {
                                        throw new NotSupportedException("Too many values for EnumOperation.Equals. Please limit to 3.");
                                    }
                                    break;

                                case EnumOperation.Contains:
                                    if (field == "name")
                                        query = query.Where(p => esp.Value.Contains(p.Index.Name));
                                    else if (field == "description")
                                        query = query.Where(p => esp.Value.Contains(p.Index.Description));
                                    else if (field == "status")
                                        query = query.Where(p => esp.Value.Contains(p.Index.Status));
                                    else
                                        throw new NotSupportedException($"Field {field} is not supported for enum contains search.");
                                    break;

                                default:
                                    throw new NotSupportedException($"Enum operation {esp.Operation} not supported");
                            }
                            break;

                        case DateSearchParameter dsp:
                            throw new NotSupportedException($"Date operation {dsp.Operation} not supported on ProjectIndex");

                        default:
                            throw new NotSupportedException($"SearchParameter type {sp.GetType().Name} not supported");
                    }
                }
            }

            // Filter by type
            query = query.Where(p => p.Type == "project-index");

            // Sorting
            if (searchRequest.Sort != null && searchRequest.Sort.Count > 0)
            {
                IOrderedQueryable<ProjectIndex>? orderedQuery = null;
                foreach (var sortField in searchRequest.Sort)
                {
                    var sortFieldLower = sortField.Field.ToLowerInvariant();
                    if (orderedQuery == null)
                    {
                        if (sortFieldLower == "name")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? query.OrderByDescending(p => p.Index.Name)
                                : query.OrderBy(p => p.Index.Name);
                        else if (sortFieldLower == "description")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? query.OrderByDescending(p => p.Index.Description)
                                : query.OrderBy(p => p.Index.Description);
                        else if (sortFieldLower == "status")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? query.OrderByDescending(p => p.Index.Status)
                                : query.OrderBy(p => p.Index.Status);
                        else
                            throw new NotSupportedException($"Sort field {sortFieldLower} is not supported.");
                    }
                    else
                    {
                        if (sortFieldLower == "name")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? orderedQuery.ThenByDescending(p => p.Index.Name)
                                : orderedQuery.ThenBy(p => p.Index.Name);
                        else if (sortFieldLower == "description")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? orderedQuery.ThenByDescending(p => p.Index.Description)
                                : orderedQuery.ThenBy(p => p.Index.Description);
                        else if (sortFieldLower == "status")
                            orderedQuery = sortField.Order == SortOrder.Desc
                                ? orderedQuery.ThenByDescending(p => p.Index.Status)
                                : orderedQuery.ThenBy(p => p.Index.Status);
                        else
                            throw new NotSupportedException($"Sort field {sortFieldLower} is not supported.");
                    }
                }
                query = orderedQuery ?? query;
            }
            else
            {
                query = query.OrderBy(p => p.Index.Name); // Default sort
            }

            // Pagination (simulate Cosmos continuation token with Take)
            int pageSize = searchRequest.PageSize ?? 100;
            var itemIds = new List<string>();
            using (var iterator = query.Take(pageSize).ToFeedIterator())
            {
                while (iterator.HasMoreResults)
                {
                    foreach (var doc in await iterator.ReadNextAsync())
                    {
                        if (doc.ItemId != null) // Prevents CS8604
                            itemIds.Add(doc.ItemId);
                    }
                }
            }

            if (itemIds.Count == 0)
                return (new List<Project>(), null);

            // Fetch projects by itemIds
            var projectsQuery = _projectsContainer.GetItemLinqQueryable<Project>(allowSynchronousQueryExecution: true)
                .Where(p => itemIds.Contains(p.Id) && p.Type == "project");

            var results = new List<Project>();
            using (var iterator = projectsQuery.ToFeedIterator())
            {
                while (iterator.HasMoreResults)
                {
                    foreach (var project in await iterator.ReadNextAsync())
                    {
                        results.Add(project);
                    }
                }
            }
            // Order results according to itemIds
            var resultsDict = results.ToDictionary(p => p.Id, p => p);
            var orderedResults = itemIds.Select(id => resultsDict.TryGetValue(id, out var project) ? project : null)
                                        .Where(p => p != null)
                                        .ToList();

            string? newContinuationToken = null; // Implement if you want real continuation tokens

            return (orderedResults!, newContinuationToken);
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
                                // Partially match for any substring
                                case StringOperation.Equals:
                                    filters.Add($"c.index.{field} = {paramName}");
                                    parameters[paramName] = ssp.Value;
                                    break;
                                case StringOperation.Contains:
                                    {
                                        var tokens = LowercaseAsciiFoldingAnalyzer.AnalyzeText(ssp.Value.ToString());
                                        var orConditions = new List<string>();
                                        for (int j = 0; j < tokens.Length; j++)
                                        {
                                            string tokenParam = $"{paramName}_{j}";
                                            orConditions.Add($"CONTAINS(t, {tokenParam})");
                                            parameters[tokenParam] = tokens[j];
                                        }

                                        string orClause = string.Join(" OR ", orConditions);
                                        filters.Add($@"
                                            EXISTS (
                                                SELECT VALUE t
                                                FROM t IN c.index.{field}_ft
                                                WHERE {orClause}
                                            )");
                                        break;
                                    }
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

            _logger.LogInformation($"SQL query: {sql}");
            string jsonString = JsonSerializer.Serialize(parameters);
            _logger.LogInformation($"parameters: {jsonString}");

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
