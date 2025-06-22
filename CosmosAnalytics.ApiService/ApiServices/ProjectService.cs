using System.Text.Json;
using System.Text.Json.Serialization;
using CosmosAnalytics.ApiService.Data;
using FakeData;
using Microsoft.Azure.Cosmos;
using ProjectModels;
using CosmosAnalytics.ApiService;
using System.IO.Compression;
using System.Text;
using ZstdNet;
using YamlDotNet.Serialization.EventEmitters;

namespace ApiServices;

public class ProjectService
{
    private readonly ProjectRepository _repository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ProjectRepository repository, ILogger<ProjectService> logger)
    {
        _repository = repository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<int> GenerateSampleProject(int size)
    {
        var faker = new CustomProjectFaker();
        var projects = new List<Project>();
        for (int i = 0; i < size; i++)
        {
            var project = faker.Generate();
            var inserted = await _repository.AddProjectAsync(project);
            projects.Add(project);
        }
        return size;
    }


    public async Task<Project> AddProjectAsync(Project project)
    {
        try
        {
            var inserted = await _repository.AddProjectAsync(project);
            _logger.LogInformation("Inserted project: {Id}", project.Id);
            return inserted;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to insert project");
            throw;
        }
    }

    public async Task<string> ExportAllProjectsAsync(bool useZstd = false)
    {
        var allItems = new List<JsonElement>();
        string? continuationToken = null;
        const int pageSize = 100;

        do
        {
            var response = await GetRawProjectsAsync(pageSize, continuationToken);
            allItems.AddRange(response.Items);
            continuationToken = response.ContinuationToken;
        } while (!string.IsNullOrEmpty(continuationToken));

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = useZstd ? ".jsonl.zst" : ".jsonl";
        var filename = $"export_projects_{timestamp}{extension}";
        Stream stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (useZstd)
        {
            stream = new CompressionStream(stream);
        }

        using var writer = new StreamWriter(stream, utf8NoBom);

        foreach (var item in allItems)
        {
            string json = item.GetRawText();
            await writer.WriteLineAsync(json);
        }

        writer.Close();

        _logger.LogInformation("Exported {Count} projects to {Filename}", allItems.Count, filename);
        return filename;
    }

    public async Task<PaginatedResponse<JsonElement>> GetRawProjectsAsync(
        int? pageSize, string? continuationToken)
    {
        var (items, token) = await _repository.GetRawProjectsAsync(pageSize, continuationToken);
        return new PaginatedResponse<JsonElement>(items, token, items.Count);
    }

    public async Task<PaginatedResponse<Project>> GetProjectsAsync(
        int? pageSize, string? continuationToken)
    {
        var (items, token) = await _repository.GetProjectsAsync(pageSize, continuationToken);
        return new PaginatedResponse<Project>(items, token, items.Count);
    }
}
