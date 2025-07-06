using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json.Serialization;

// Define custom attributes
[AttributeUsage(AttributeTargets.Property)]
public class SearchableAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class IndexedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class IndexedFullTextAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class KeyAttribute : Attribute { }

public class SearchableSchemaFilter : ISchemaFilter
{
    // Map attribute type to OpenAPI extension name
    private static readonly Dictionary<Type, string> AttributeExtensionMap = new()
    {
        { typeof(SearchableAttribute), "x-searchable" },
        { typeof(IndexedAttribute), "x-indexed" },
        { typeof(IndexedFullTextAttribute), "x-indexed-fulltext" },
        { typeof(KeyAttribute), "x-key" }
    };

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var property in context.Type.GetProperties())
        {
            var propertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            if (!schema.Properties.ContainsKey(propertyName))
                continue;

            foreach (var attr in AttributeExtensionMap)
            {
                if (property.GetCustomAttribute(attr.Key) != null)
                {
                    // Add extension if not already present
                    if (!schema.Properties[propertyName].Extensions.ContainsKey(attr.Value))
                    {
                        schema.Properties[propertyName].Extensions.Add(attr.Value, new Microsoft.OpenApi.Any.OpenApiBoolean(true));
                    }
                }
            }
        }
    }
}
