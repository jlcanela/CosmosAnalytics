using System.Linq;
using Jolt.Net;
using Newtonsoft.Json.Linq;

public class FtTransform : SpecDriven, ITransform
{
    JToken _spec;
    public string[] _fields;
    public FtTransform(JToken spec)
    {
        _spec = spec;

        JToken fieldsToken = spec["fields"];
        if (fieldsToken != null && fieldsToken.Type == JTokenType.Array)
        {
            _fields = fieldsToken.ToObject<string[]>();
        }
        else
        {
            _fields = new string[0]; // Empty array if "fields" not present
        }
    }

    public JToken Transform(JToken input)
    {
        // Clone the input to avoid modifying the original
        var output = input.DeepClone();

        // Ensure output is a JObject
        if (output is JObject obj)
        {
            // Ensure "index" exists and is a JObject
            if (obj["index"] is JObject indexObj)
            {
                foreach (var field in _fields)
                {
                    // Check if the field exists under "index"
                    if (indexObj.TryGetValue(field, out JToken fieldValue))
                    {
                        string[] tokens = LowercaseAsciiFoldingAnalyzer.AnalyzeText(fieldValue.ToString());
                        // Duplicate the field with _ft suffix
                        indexObj[$"{field}_ft"] = new JArray(tokens);
                    }
                }
            }
        }

        return output;
    }
}
