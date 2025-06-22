using ApiServices;
using CosmosAnalytics.ApiService.Data;
using Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Azure.Cosmos;
using System.Text.Json.Serialization;

namespace CosmosAnalytics.ApiService
{

    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            var builder = WebApplication.CreateBuilder(args);

            builder.AddServiceDefaults();
            builder.Services.AddProblemDetails();
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<ProjectService>();
            builder.Services.AddSingleton<ReportingService>();

            builder.AddAzureCosmosClient("projects",
                configureClientOptions: options =>
                {
                    options.UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    };
                    options.UseSystemTextJsonSerializerWithOptions.Converters.Add(new JsonStringEnumConverter());
                });

            builder.Services.AddSingleton(serviceProvider =>
            {
                var client = serviceProvider.GetRequiredService<CosmosClient>();
                return client.GetDatabase("cosmosdb").GetContainer("projects");
            });

            builder.Services.AddOpenApi();
            builder.Services.AddSwaggerUI();
         

            var app = builder.Build();

            app.UseExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapSwaggerUI();
            }

            app.MapGet("/", () =>
            {
                string markdown = File.ReadAllText("README.md");
                string documentation = Markdig.Markdown.ToHtml(markdown);
                return Results.Content(documentation, "text/html");
            })
            .ExcludeFromDescription();

            // Register endpoints from the extracted controller
            app.MapProjectEndpoints();

            app.MapDefaultEndpoints();

            app.Run();
        }
    }
}
