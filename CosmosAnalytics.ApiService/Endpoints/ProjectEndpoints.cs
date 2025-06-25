using ApiServices;
using CosmosAnalytics.ApiService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using ProjectModels;
using System.Text.Json;

namespace Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        app.MapPost("/generate-sample", async (
            ProjectService service,
            ILogger<ProjectService> logger,
            int size = 1) =>
        {
            try
            {
                var count = await service.GenerateSampleProject(size);
                return Results.Json(count);
            }
            catch (CosmosException)
            {
                return Results.Problem("Failed to save project to database");
            }
        }).WithName("PostGenerateSample")
        .Produces<int>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/project-items", async (
            ProjectService service,
            [FromQuery] int? pageSize,
            [FromQuery] string? continuationToken) =>
        {
            try
            {
                var response = await service.GetRawProjectsAsync(pageSize, continuationToken);
                return Results.Ok(response);
            }
            catch (CosmosException)
            {
                return Results.Problem("Failed to retrieve projects from database");
            }
        })
        .Produces<PaginatedResponse<JsonElement>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/projects", async (
            ProjectService service,
            [FromQuery] int? pageSize,
            [FromQuery] string? continuationToken) =>
        {
            try
            {
                var response = await service.GetProjectsAsync(pageSize, continuationToken);
                return Results.Ok(response);
            }
            catch (CosmosException)
            {
                return Results.Problem("Failed to retrieve projects from database");
            }
        })
        .Produces<PaginatedResponse<Project>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/export-all", async (ProjectService service,
         [FromQuery] bool compressed,
            [FromQuery] bool useStorageAccount) =>
        {
            try
            {
                var filename = await service.ExportAllProjectsAsyncSimple(useStorageAccount, compressed);
                return Results.Text(filename, "text/plain");

            }
            catch (Exception ex)
            {
                return Results.Problem($"Export failed: {ex.Message}");
            }
        })
        .Produces<string>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/exports", async (ProjectService service) =>
        {
            var exports = await service.ListBlobFilesAsync();
            return Results.Ok(exports);
        })
        .Produces<List<string>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);


        app.MapGet("/report", async (
         [FromQuery] string filename,
         [FromQuery] string? sqlQuery,
         [FromQuery] bool useStorageAccount,
         [FromServices] ReportingService reportingService) =>  // Add [FromServices] here
         {
             try
             {
                 // Use default query if none provided
                 var query = string.IsNullOrWhiteSpace(sqlQuery)
                 ? @"SELECT status, COUNT(*) as count FROM projects GROUP BY status"
                 : sqlQuery;

                 var results = await reportingService.RunReportAsync(filename, query, useStorageAccount);
                 return Results.Ok(results);
             }
             catch (Exception ex)
             {
                 return Results.Problem($"Report failed: {ex.Message}");
             }
         });

    }
}
