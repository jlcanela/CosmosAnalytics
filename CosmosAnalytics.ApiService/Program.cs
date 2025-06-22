using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;
using AutoBogus;
using ProjectModels;
using FakeData;
using Markdig;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}


app.MapGet("/", () =>
{
  string markdown = File.ReadAllText("README.md");
  string documentation = Markdown.ToHtml(markdown);
  return Results.Content(documentation, "text/html");
});

var options = new JsonSerializerOptions
{
  WriteIndented = true,
};
options.Converters.Add(new JsonStringEnumConverter());


app.MapGet("/generate-sample", () =>
{
  var project = new CustomProjectFaker().Generate(); // AutoFaker.Generate<Project>();
  string json = JsonSerializer.Serialize(project, options);
  return json;
})
.WithName("GetGenerateSample");


app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
  public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
