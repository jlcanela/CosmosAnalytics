using System.Text.Json;
using CosmosAnalytics.ApiService.Data;
using FakeData;
using Microsoft.Azure.Cosmos;
using ProjectModels;
using CosmosAnalytics.ApiService;
using System.Text;
using Azure.Storage.Blobs;

namespace ApiServices;

public class ProjectService
{
    private readonly ProjectRepository _repository;
    private readonly ILogger<ProjectService> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public ProjectService(ProjectRepository repository, ILogger<ProjectService> logger, BlobServiceClient blobServiceClient)
    {
        _repository = repository;
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<int> GenerateSampleProject(int size)
    {
        var faker = new CustomProjectFaker();
        var projects = new List<Project>();
        for (int i = 0; i < size; i++)
        {
            var project = faker.Generate();
            projects.Add(project);
        }
        var res = await _repository.AddProjectsBulkAsync(projects);
        return res.Count;
    }

    public async Task<int> GenerateSampleProjectSimple(int size)
    {
        var faker = new CustomProjectFaker();
        for (int i = 0; i < size; i++)
        {
            var project = faker.Generate();
            await _repository.AddProjectAsync(project);

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

    public async Task<int> ExportAllProjects(Stream outputStream, Encoding encoding)
    {
        int totalCount = 0;
        string? continuationToken = null;
        const int pageSize = 100;

        using var writer = new StreamWriter(outputStream, encoding, leaveOpen: true);

        do
        {
            var response = await _repository.GetRawProjectsAsync(pageSize, continuationToken);
            continuationToken = response.ContinuationToken;

            foreach (var item in response.Items)
            {
                // Write each item as a JSON line
                var jsonString = item.GetRawText();
                await writer.WriteLineAsync(jsonString);
                totalCount++;
            }

        } while (!string.IsNullOrEmpty(continuationToken));

        await writer.FlushAsync();
        return totalCount;
    }

    public async Task<string> ExportAllProjectsAsyncSimple(bool useStorageAccount = false, bool useZstd = false)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = useZstd ? ".jsonl.zst" : ".jsonl";
        var filename = $"export_projects_{timestamp}{extension}";

        // Single command to create, write, and close
        await using (var export = await ExportStream.CreateAsync(
            filename,
            useStorageAccount,
            useZstd,
            _blobServiceClient))
        {
            await ExportAllProjects(export.Stream, utf8NoBom);
        }
        return filename;
    }

    public async Task<List<string>> ListBlobFilesAsync()
    {
        var container = _blobServiceClient.GetBlobContainerClient("upload-container");
        var blobs = new List<string>();

        await foreach (var blobItem in container.GetBlobsAsync())
        {
            blobs.Add(blobItem.Name);
        }

        return blobs;
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
