using ApiServices;
using Azure.Storage.Blobs;
using CosmosAnalytics.ApiService.Data;
using Endpoints;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace CosmosAnalytics.ApiService
{

    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            var builder = WebApplication.CreateBuilder(args);
            string storageConnectionString = builder.Configuration.GetConnectionString("upload-container")!;

            builder.AddServiceDefaults();
            builder.Services.AddProblemDetails();
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<ProjectService>();
            builder.Services.AddSingleton<ReportingService>();
            builder.Services.AddSingleton(new BlobServiceClient(storageConnectionString));

            builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

            builder.AddAzureCosmosClient("projects",
                configureClientOptions: options =>
                {
                    options.UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions()
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    };
                    options.UseSystemTextJsonSerializerWithOptions.Converters.Add(new JsonStringEnumConverter());
                });

            builder.Services.AddSingleton<CosmosIndexingPolicyService>();
            builder.Services.AddSingleton(serviceProvider =>
            {
                var client = serviceProvider.GetRequiredService<CosmosClient>();
                var db = client.GetDatabase("cosmosdb");
                var containers = new CosmosContainers(
                    db.GetContainer("projects"),
                    db.GetContainer("index")
                        );
                return containers;
            });

            // builder.Services.AddHostedService<StartupOrHostedService>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SchemaFilter<SearchableSchemaFilter>();
                c.SchemaFilter<StringEnumSchemaFilter>();
                c.UseAllOfToExtendReferenceSchemas(); // Optional: for better inheritance support
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("LocalhostPolicy", policy =>
                {
                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            // Allow http(s)://localhost:anyport
                            if (origin is null) return false;
                            return origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:");
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials(); // Optional: if you need cookies/auth
                });
            });

            var app = builder.Build();
   

            app.MapGet("/swagger.help", () =>
            {
                string markdown = File.ReadAllText("README.md");
                string documentation = Markdig.Markdown.ToHtml(markdown);
                return Results.Content(documentation, "text/html");
            })
            .ExcludeFromDescription();

            // Serve SPA static files
            var spaPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../CosmosAnalytics.Web/dist/"));
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(spaPath),
                RequestPath = ""
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(spaPath),
                RequestPath = ""
            });

            app.UseRouting();
            app.UseCors("LocalhostPolicy");

            var api = app.MapGroup("/api");
            // Register endpoints from the extracted controller
            api.MapProjectEndpoints();
            app.MapDefaultEndpoints();
            app.MapFallbackToFile("index.html");

            app.UseExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.Run();
        }
    }
}
