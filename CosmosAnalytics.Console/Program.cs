// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using Jolt.Net;

JToken indexSpec = JToken.Parse(@"
      [
    {
        ""operation"": ""shift"",
        ""spec"": {
            ""rating"": {
                ""primary"": {
                    ""value"": ""Rating""
                },
                ""*"": {
                    ""value"": ""SecondaryRatings.&1.Value"",
                    ""$"": ""SecondaryRatings.&.Id""
                }
            }
        }
    },
    {
        ""operation"": ""default"",
        ""spec"": {
            ""Range"" : 5,
            ""SecondaryRatings"" : {
                ""*"" : {
                    ""Range"" : 5
                }
            }
        }
    }
]");

var chainr = Chainr.FromSpec(indexSpec);

var json2 = @"{
    ""rating"": {
        ""primary"": {
            ""value"": 3
        },
        ""quality"": {
            ""value"": 3
        }
    }
}";

var output = chainr.Transform(JToken.Parse(json2));

Console.WriteLine(output.ToString());