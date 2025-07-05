using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntitySearchApi.Models
{
    // Supported operations as enums
    public enum StringOperation { Equals, Contains, StartsWith, EndsWith }
    public enum EnumOperation { Equals, Contains }
    public enum NumberOperation { Equals, GreaterThan, GreaterEqualThan, LessThan, LessEqualThan }
    public enum NumberRangeOperation { Between }
    public enum DateOperation { Before, After }
    public enum DateRangeOperation { Between }
    public enum UniversalOperation { Exists, NotExists }
    public enum SortOrder { Asc, Desc }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(StringSearchParameter), "string")]
    [JsonDerivedType(typeof(EnumSearchParameter), "enum")]
    [JsonDerivedType(typeof(NumberSearchParameter), "number")]
    [JsonDerivedType(typeof(NumberRangeSearchParameter), "numberRange")]
    [JsonDerivedType(typeof(DateSearchParameter), "date")]
    [JsonDerivedType(typeof(DateRangeSearchParameter), "dateRange")]
    [JsonDerivedType(typeof(UniversalSearchParameter), "universal")]
    public abstract class SearchParameter
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = default!;
    }

    // String search
    public class StringSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public StringOperation Operation { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; } = default!;
    }

    // Enum search
    public class EnumSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public EnumOperation Operation { get; set; }

        [JsonPropertyName("value")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Value { get; set; } = new();
    }

    // Number search
    public class NumberSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public NumberOperation Operation { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    // Number range search
    public class NumberRangeSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public NumberRangeOperation Operation { get; set; }

        [JsonPropertyName("value")]
        public List<double> Value { get; set; } = new(2);
    }

    // Date search
    public class DateSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public DateOperation Operation { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; } = default!; // ISO 8601
    }

    // Date range search
    public class DateRangeSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public DateRangeOperation Operation { get; set; }

        [JsonPropertyName("value")]
        public List<string> Value { get; set; } = new(2); // [from, to], ISO 8601
    }

    // Universal search (exists/notExists)
    public class UniversalSearchParameter : SearchParameter
    {
        [JsonPropertyName("operation")]
        public UniversalOperation Operation { get; set; }
    }

    // Sorting
    public class SortField
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = default!;

        [JsonPropertyName("order")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SortOrder? Order { get; set; }
    }

    // Main search request
    public class EntitySearchRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;

        [JsonPropertyName("searchParameters")]
        public List<SearchParameter>? SearchParameters { get; set; }

        [JsonPropertyName("pageSize")]
        public int? PageSize { get; set; }

        [JsonPropertyName("continuationToken")]
        public string? ContinuationToken { get; set; }

        [JsonPropertyName("sort")]
        public List<SortField>? Sort { get; set; }

        [JsonPropertyName("fields")]
        public List<string>? Fields { get; set; }

        [JsonPropertyName("count")]
        public bool? Count { get; set; }

        [JsonPropertyName("facets")]
        public List<string>? Facets { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    // Facet result
    public class FacetResult
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = default!;

        [JsonPropertyName("counts")]
        public Dictionary<string, int> Counts { get; set; } = new();
    }

    // Generic search response
    public class EntitySearchResponse<T>
    {
        [JsonPropertyName("results")]
        public List<T> Results { get; set; } = new();

        [JsonPropertyName("totalCount")]
        public int? TotalCount { get; set; }

        [JsonPropertyName("continuationToken")]
        public string? ContinuationToken { get; set; }

        [JsonPropertyName("facets")]
        public List<FacetResult>? Facets { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    // Error response
    public class ErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = default!;
    }

    // Helper: Accept single value or array for enum search
    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new List<T>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    result.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
                }
            }
            else
            {
                result.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            if (value.Count == 1)
            {
                JsonSerializer.Serialize(writer, value[0], options);
            }
            else
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}
