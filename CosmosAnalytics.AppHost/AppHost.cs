var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CosmosAnalytics_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
