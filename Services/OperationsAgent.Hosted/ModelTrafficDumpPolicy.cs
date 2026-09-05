using System.ClientModel.Primitives;
using System.Text;

namespace OperationsAgent.Hosted;

/// <summary>
/// Writes every outbound project data-plane request body to a directory, one numbered file per
/// request, named by sequence and response status so a rejected POST is identifiable at a glance.
/// </summary>
/// <remarks>
/// Exists because of the hosted runtime's invalid_payload defect: the service reports only "The
/// provided data does not match the expected schema", with no parameter and no offending value, and
/// the request that earned it is assembled deep inside the SDK from streamed updates and rehydrated
/// history. The only way to see what was actually sent is to capture it at the pipeline, and the only
/// place that capture can be composed is here, where the AIProjectClient is constructed.
///
/// Registered only when DUMP_MODEL_TRAFFIC names a directory, and never in the Foundry-hosted
/// environment - Program.cs refuses the flag there, because the dumps contain full prompts, tool
/// outputs and whatever a tool returned about a person's own documents, and a hosted container's
/// persistent filesystem is no place to leave them.
/// </remarks>
internal sealed class ModelTrafficDumpPolicy(string dumpDirectory) : PipelinePolicy
{
    private int _sequence;

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        => ProcessAsync(message, pipeline, currentIndex).AsTask().GetAwaiter().GetResult();

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        string? body = null;

        try
        {
            if (message.Request.Content is not null)
            {
                using var buffer = new MemoryStream();
                await message.Request.Content.WriteToAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                body = Encoding.UTF8.GetString(buffer.ToArray());
            }
        }
        catch (Exception exception)
        {
            // Best-effort for the same reason the write below is: this side runs before the
            // request is sent, and a capture that stops it from being sent has inverted the
            // diagnostic's purpose. The failure itself becomes the dump's content.
            body = $"(body capture failed: {exception.GetType().Name}: {exception.Message})";
        }

        // Captured before the call: the URI is what was asked for even if the request then fails.
        var uri = message.Request.Uri!;

        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);

        try
        {
            var status = message.Response?.Status ?? 0;
            var path = uri.AbsolutePath.Replace('/', '_');
            if (path.Length > 80)
            {
                path = path[^80..];
            }

            Directory.CreateDirectory(dumpDirectory);
            var name = $"{sequence:D3}_{message.Request.Method}_{status}{path}.json";
            // Deliberately not the message's token: a dump of a cancelled request is exactly the
            // evidence worth keeping.
            await File.WriteAllTextAsync(Path.Combine(dumpDirectory, name), body ?? "(no body)", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // A dump that cannot be written must never take the response down with it: by this
            // point the model has answered, and an unwritable directory is a fact about the
            // filesystem, not about the request. A pipeline policy is composed before the host
            // and its logging exist, so there is no ILogger to reach - stderr is where the loss
            // is reported.
            await Console.Error.WriteLineAsync(
                $"ModelTrafficDumpPolicy: dump {sequence:D3} for {uri.AbsolutePath} not written to '{dumpDirectory}': {exception.GetType().Name}: {exception.Message}")
                .ConfigureAwait(false);
        }
    }
}
