using System;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Orchestrator.Infrastructure.IdRewriter;

/// <summary>
/// Utility for processing a UTF-8 JSON stream token by token, writing transformed output to a target stream.
/// </summary>
/// <remarks>
/// Based on the equivalent implementation in iiif-presentation. Any changes here should be considered there too.
/// </remarks>
public static class StreamingJsonProcessor
{
    /// <summary>
    /// Reads <paramref name="input" /> UTF-8 JSON stream token by token and writes it to
    /// <paramref name="output" /> as UTF-8 JSON, optionally transforming tokens on the fly via
    /// <paramref name="implementation" />.
    /// </summary>
    /// <param name="input">UTF-8 JSON source stream</param>
    /// <param name="output">Processed UTF-8 JSON target stream</param>
    /// <param name="inputLength">Content length, if known; helps size the initial read buffer</param>
    /// <param name="implementation">Token handler that performs any desired transformations</param>
    /// <param name="log">Logger for the static method; pass null to suppress logging</param>
    /// <remarks>
    /// In C# 13 this could be made async, but ref structs currently do not work with async/await.
    /// </remarks>
    public static void ProcessJson(Stream input, Stream output, long? inputLength, IJsonProcessor implementation,
        ILogger? log = null)
    {
        const int bufferSize = 1024;
        var initialSize = inputLength.HasValue ? (int) Math.Min(inputLength.Value, bufferSize) : bufferSize;
        Span<byte> buffer = new byte[initialSize];

        input.ReadExactly(buffer);
        var totalRead = (long) buffer.Length;

        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true, SkipValidation = true });

        var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: default);
        var currentState = implementation.GetInitialState();

        while (true)
            try
            {
                if (!reader.Read())
                {
                    if (reader.IsFinalBlock)
                        return;

                    var shouldContinue =
                        GetMoreBytesFromStream(input, ref buffer, ref reader, inputLength, ref totalRead);
                    if (!shouldContinue)
                        return;
                }
                else
                {
                    implementation.OnToken(ref reader, writer, ref currentState);
                }
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "Error while processing stream");
                throw;
            }
    }

    private static bool GetMoreBytesFromStream(Stream input, ref Span<byte> buffer, ref Utf8JsonReader reader,
        long? inputLength, ref long totalRead)
    {
        var remainingStreamBytes = (int) ((inputLength ?? input.Length) - totalRead);
        if (remainingStreamBytes == 0)
            return false;

        var finalRead = false;
        if (reader.BytesConsumed < buffer.Length)
        {
            ReadOnlySpan<byte> leftover = buffer[(int) reader.BytesConsumed..];
            var leftoverBytes = leftover.Length;

            if (leftover.Length == buffer.Length)
            {
                var newLength = buffer.Length * 2;
                if (newLength >= remainingStreamBytes + leftoverBytes)
                {
                    newLength = remainingStreamBytes + leftoverBytes;
                    finalRead = true;
                }

                var temp = new byte[newLength];
                buffer.CopyTo(temp);
                buffer = temp;
            }
            else if (buffer.Length > leftoverBytes + remainingStreamBytes)
            {
                buffer = buffer[..(leftoverBytes + remainingStreamBytes)];
                finalRead = true;
            }

            leftover.CopyTo(buffer);
            input.ReadExactly(buffer[leftoverBytes..]);
            totalRead += buffer.Length - leftoverBytes;
        }
        else
        {
            if (remainingStreamBytes < buffer.Length)
            {
                buffer = buffer[..remainingStreamBytes];
                finalRead = true;
            }

            input.ReadExactly(buffer);
            totalRead += buffer.Length;
        }

        reader = new Utf8JsonReader(buffer, isFinalBlock: finalRead, state: reader.CurrentState);
        return true;
    }
}
