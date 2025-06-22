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
        app.MapGet("/generate-sample", async (
            ProjectService service,
            ILogger<ProjectService> logger) =>
        {
            try
            {
                var project = service.GenerateSampleProject();
                var inserted = await service.AddProjectAsync(project);
                return Results.Json(inserted);
            }
            catch (CosmosException)
            {
                return Results.Problem("Failed to save project to database");
            }
        }).WithName("GetGenerateSample");

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
    }
}
