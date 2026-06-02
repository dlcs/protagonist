using System;
using System.Text.Json;

namespace Orchestrator.Infrastructure.IdRewriter;

/// <summary>
/// <see cref="IJsonProcessor" /> implementation that rewrites the top-level <c>id</c> property of a JSON document
/// to a caller-supplied value. If the document has no top-level <c>id</c> property, one is injected before the
/// root object is closed.
/// </summary>
public class TopLevelIdRewriteProcessor(string newId)
    : StreamingProcessorBase<TopLevelIdRewriteProcessor.ProcessorState>
{
    private const string IdPropertyName = "id";

    public override object GetInitialState() => new ProcessorState();

    protected override void OnPropertyName(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref ProcessorState currentState)
    {
        currentState.PropertyName = reader.GetString()!;
        writer.WritePropertyName(currentState.PropertyName);
    }

    protected override void OnString(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref ProcessorState currentState)
    {
        currentState.Depth = reader.CurrentDepth;
        writer.WriteStringValue(RewriteIdIfNeeded(reader.GetString(), ref currentState));
    }

    protected override void OnEndObject(ref Utf8JsonReader reader, Utf8JsonWriter writer,
        ref ProcessorState currentState)
    {
        if (reader.CurrentDepth == 0 && !currentState.IdWritten)
        {
            writer.WritePropertyName(IdPropertyName);
            writer.WriteStringValue(newId);
        }

        base.OnEndObject(ref reader, writer, ref currentState);
    }

    private string? RewriteIdIfNeeded(string? value, ref ProcessorState currentState)
    {
        if (currentState.Depth == 1 &&
            IdPropertyName.Equals(currentState.PropertyName, StringComparison.OrdinalIgnoreCase))
        {
            currentState.IdWritten = true;
            return newId;
        }

        return value;
    }

    public class ProcessorState
    {
        public string? PropertyName { get; set; }
        public int Depth { get; set; }
        public bool IdWritten { get; set; }
    }
}
