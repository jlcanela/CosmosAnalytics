
public class FtTransform: SpecDriven, ITransform
{
    JToken _spec;
    public FtTransform(JToken spec)
    {
        _spec = spec;
    }

    public JToken Transform(JToken input)
    {
        // Implement your custom logic here.
        // For example, let's say you want to add a "fullText" field to the output.
        var output = input.DeepClone();

        // Example: concatenate name and description as a fullText field
        var name = input["name"]?.ToString() ?? "";
        var description = input["description"]?.ToString() ?? "";
        var fullText = $"{name} {description}".Trim();

        if (output is JObject obj)
        {
            obj["fullText"] = fullText;
        }

        return output;
    }
}