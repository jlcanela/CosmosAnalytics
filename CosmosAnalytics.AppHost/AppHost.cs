var builder = DistributedApplication.CreateBuilder(args);

// Add CosmosDB emulator
#pragma warning disable ASPIRECOSMOSDB001 
var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsPreviewEmulator((emulator =>
    {
        emulator
        .WithHttpEndpoint(targetPort: 1234, name: "explorer-port", isProxied: true);
        // .WithLifetime(ContainerLifetime.Persistent);
    }));
#pragma warning restore ASPIRECOSMOSDB001

var cosmosdb = cosmos.AddCosmosDatabase("cosmosdb");
var container = cosmosdb.AddContainer("projects", "/id"); // Partition key path

builder.AddProject<Projects.CosmosAnalytics_ApiService>("apiservice")
    .WithReference(container)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
