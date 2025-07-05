var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Storage emulator (Azurite)
// var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator =>
    {
        // Enable Azurite's embedded UI
        emulator.WithEnvironment("ENABLE_ACCESS_LOG", "true")
                .WithEnvironment("ENABLE_DEBUG_LOG", "true")
                .WithEnvironment("ENABLE_UI", "true");
        
        // Expose UI port
        emulator.WithEndpoint(
            targetPort: 10002, 
            name: "ui-port", 
            scheme: "http"
        );
    });

// Add blob container for file uploads
var blobs = storage.AddBlobs("upload-container");

// Add CosmosDB emulator
#pragma warning disable ASPIRECOSMOSDB001 
var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator((emulator =>
    {
        emulator
        .WithHttpsEndpoint(targetPort: 1234, name: "explorer-port", isProxied: true)
        .WithLifetime(ContainerLifetime.Persistent);
    }));
#pragma warning restore ASPIRECOSMOSDB001

var cosmosdb = cosmos.AddCosmosDatabase("cosmosdb");
var container = cosmosdb.AddContainer("projects", "/id"); // Partition key path


var api = builder.AddProject<Projects.CosmosAnalytics_ApiService>("apiservice")
    .WithReference(container)
    .WithReference(blobs)
    .WithHttpHealthCheck("/health");

var web = builder.AddPnpmApp(name: "Web",
        workingDirectory: "..",
        scriptName: "webdev"
    )
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithPnpmPackageInstallation()
    .WithReference(api)
    .WaitFor(api); 

builder.Build().Run();
