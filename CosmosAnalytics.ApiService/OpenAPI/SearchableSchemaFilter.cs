using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json.Serialization;

public class SearchableSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var property in context.Type.GetProperties())
        {
            if (property.GetCustomAttribute<SearchableAttribute>() != null)
            {
                var propertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                if (schema.Properties.ContainsKey(propertyName))
                {
                    schema.Properties[propertyName].Extensions.Add("x-searchable", new Microsoft.OpenApi.Any.OpenApiBoolean(true));
                }
            }
        }
    }
}
