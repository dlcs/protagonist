using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Orchestrator.Infrastructure.IdRewriter;

namespace Orchestrator.Tests.Infrastructure.IdRewriter;

public class TopLevelIdRewriteProcessorTests
{
    private const string NewId = "https://example.org/iiif/item/1";

    [Fact]
    public void ProcessJson_RewritesExistingTopLevelId()
    {
        const string input = """
            {
              "id": "https://old.example.org/manifest/1",
              "type": "AnnotationPage",
              "items": []
            }
            """;

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetString().Should().Be(NewId);
    }

    [Fact]
    public void ProcessJson_InjectsIdWhenAbsent()
    {
        const string input = """
            {
              "type": "AnnotationPage",
              "items": []
            }
            """;

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetString().Should().Be(NewId);
    }

    [Fact]
    public void ProcessJson_DoesNotRewriteNestedIdProperties()
    {
        const string nestedId = "https://old.example.org/canvas/1";
        var input = $$"""
            {
              "id": "https://old.example.org/manifest/1",
              "type": "AnnotationPage",
              "items": [
                {
                  "id": "{{nestedId}}",
                  "type": "Annotation"
                }
              ]
            }
            """;

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetString().Should().Be(NewId);
        doc.RootElement.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(nestedId);
    }

    [Fact]
    public void ProcessJson_PreservesAllOtherProperties()
    {
        const string input = """
            {
              "id": "https://old.example.org/manifest/1",
              "type": "AnnotationPage",
              "label": { "en": ["My Page"] },
              "items": []
            }
            """;

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("type").GetString().Should().Be("AnnotationPage");
        doc.RootElement.GetProperty("label").GetProperty("en")[0].GetString().Should().Be("My Page");
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void ProcessJson_InjectsIdIntoEmptyRootObject()
    {
        const string input = "{}";

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetString().Should().Be(NewId);
    }

    [Fact]
    public void ProcessJson_HandlesLargeDocument()
    {
        var items = string.Join(",\n", Enumerable.Range(0, 500).Select(i =>
            $$"""{ "id": "https://old.example.org/annotation/{{i}}", "type": "Annotation" }"""));
        var input = $$"""
            {
              "id": "https://old.example.org/page/1",
              "type": "AnnotationPage",
              "items": [{{items}}]
            }
            """;

        var result = Process(input);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetString().Should().Be(NewId);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(500);
    }

    private static string Process(string json)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var output = new MemoryStream();
        StreamingJsonProcessor.ProcessJson(input, output, input.Length, new TopLevelIdRewriteProcessor(NewId));
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
