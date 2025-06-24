using System.Text.Json;
using System.Text.Json.Serialization;
using CosmosAnalytics.ApiService.Data;
using FakeData;
using Microsoft.Azure.Cosmos;
using ProjectModels;
using CosmosAnalytics.ApiService;
using System.Text;
using ZstdNet;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Task = System.Threading.Tasks.Task;


namespace ApiServices;

public class ProjectService
{
    private readonly ProjectRepository _repository;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ProjectRepository repository, ILogger<ProjectService> logger)
    {
        _repository = repository;
        _logger = logger;
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

    public async Task<string> ExportAllProjectsAsync(bool useZstd = false)
    {
        const int pageSize = 100;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = useZstd ? ".jsonl.zst" : ".jsonl";
        var filename = $"export_projects_{timestamp}{extension}";
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var totalCount = 0;

        // Create processing pipeline with backpressure
        var processingChannel = Channel.CreateBounded<(List<JsonElement> Items, int PageIndex)>(10);
        var buffer = new ConcurrentDictionary<int, byte[]>();

        var writerTask = Task.Run(async () =>
        {
            await using var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write);

            // Maintain write order using page index
            var nextPageIndex = 0;

            await foreach (var (items, pageIndex) in processingChannel.Reader.ReadAllAsync())
            {
                byte[] data;
                // Wait until it's this page's turn to write
                while (!buffer.TryRemove(nextPageIndex, out data))
                {
                    if (buffer.TryGetValue(nextPageIndex, out data))
                    {
                        buffer.TryRemove(nextPageIndex, out _);
                        break;
                    }
                    await Task.Delay(10);
                }

                await fileStream.WriteAsync(data);
                Interlocked.Add(ref totalCount, items.Count);
                nextPageIndex++;
            }
        });

        try
        {
            string? continuationToken = null;
            var pageIndex = 0;

            do
            {
                // Fetch next page
                var response = await GetRawProjectsAsync(pageSize, continuationToken);
                continuationToken = response.ContinuationToken;

                // Process page in parallel (compression + serialization)
                var currentPage = response.Items;
                var currentIndex = pageIndex++;
                _ = Task.Run(() => // Fixed: removed 'async'
                {
                    var jsonLines = string.Join('\n', currentPage.Select(j => j.GetRawText()));
                    var bytes = utf8NoBom.GetBytes(jsonLines);

                    if (useZstd)
                    {
                        using (var compressor = new Compressor())
                        {
                            bytes = compressor.Wrap(bytes);
                        }
                    }

                    // Store bytes in buffer for ordered writing
                    buffer[currentIndex] = bytes;
                    processingChannel.Writer.TryWrite((currentPage, currentIndex));
                });
            } while (!string.IsNullOrEmpty(continuationToken));
        }
        finally
        {
            processingChannel.Writer.Complete();
            await writerTask;
        }

        _logger.LogInformation("Exported {Count} projects to {Filename}", totalCount, filename);
        return filename;
    }

    public async Task<string> ExportAllProjectsAsyncSimple(bool useZstd = false)
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
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using (var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write))
        {
            if (useZstd)
            {
                // Use ZstdSharp compression with explicit disposal
                using (var compressionStream = new CompressionStream(fileStream))
                using (var writer = new StreamWriter(compressionStream, utf8NoBom, leaveOpen: true))
                {
                    await WriteItemsAsync(writer, allItems);
                }
            }
            else
            {
                using (var writer = new StreamWriter(fileStream, utf8NoBom, leaveOpen: true))
                {
                    await WriteItemsAsync(writer, allItems);
                }
            }
        }

        _logger.LogInformation("Exported {Count} projects to {Filename}", allItems.Count, filename);
        return filename;
    }

    private async Task WriteItemsAsync(StreamWriter writer, List<JsonElement> items)
    {
        foreach (var item in items)
        {
            string json = item.GetRawText();
            await writer.WriteLineAsync(json);
        }
        await writer.FlushAsync();
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
