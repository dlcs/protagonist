using System.Text.Json;

namespace Orchestrator.Infrastructure.IdRewriter;

public interface IJsonProcessor
{
    /// <summary>
    /// Called once when starting to process a JSON stream.
    /// </summary>
    /// <returns>Arbitrary object that will be ref passed to <see cref="OnToken" /></returns>
    object GetInitialState();

    /// <summary>
    /// Called when a full JSON token has been read from the input stream.
    /// </summary>
    /// <param name="reader">Current reader that read the JSON token</param>
    /// <param name="writer">Matching writer for output stream</param>
    /// <param name="customState">
    /// State as initially obtained from <see cref="GetInitialState" />, or one created in previous
    /// <see cref="OnToken" /> invocations
    /// </param>
    void OnToken(ref Utf8JsonReader reader, Utf8JsonWriter writer, ref object customState);
}