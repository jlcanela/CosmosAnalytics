using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class StringEnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Type = "string";
            schema.Enum = Enum.GetNames(context.Type)
                .Select(name => new Microsoft.OpenApi.Any.OpenApiString(name))
                .Cast<Microsoft.OpenApi.Any.IOpenApiAny>()
                .ToList();
        }
    }
}
