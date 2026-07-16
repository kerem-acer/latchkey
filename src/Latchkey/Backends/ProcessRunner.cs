using System.Security.Cryptography;
using System.Text;

using CliWrap;

namespace Latchkey.Backends;

/// <summary>Result of running an external process: exit code plus captured output.</summary>
readonly struct ProcessResult
{
    public required int ExitCode { get; init; }
    public required byte[] StandardOutput { get; init; }
    public required string StandardError { get; init; }
}

/// <summary>
/// Runs an external process via CliWrap with optional binary stdin, capturing stdout as raw bytes
/// and stderr as text. Arguments are passed as a list (no shell, no quoting pitfalls); secrets are
/// piped on stdin, never on the command line. Exit-code validation is disabled so callers interpret
/// non-zero codes themselves. CliWrap drains stdin/stdout/stderr concurrently, so a large output
/// never deadlocks the pipe. It has no synchronous API, so <see cref="Run" /> blocks on the async
/// core; CliWrap awaits with <c>ConfigureAwait(false)</c> throughout, so this cannot deadlock on a
/// captured synchronization context.
/// </summary>
static class ProcessRunner
{
    internal static ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        ReadOnlySpan<byte> stdin,
        IReadOnlyDictionary<string, string?>? environment = null) =>
        Execute(
                fileName,
                arguments,
                stdin.ToArray(),
                environment,
                CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();

    internal static ValueTask<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> stdin,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken) =>
        Execute(
            fileName,
            arguments,
            stdin.ToArray(),
            environment,
            cancellationToken);

    static async ValueTask<ProcessResult> Execute(
        string fileName,
        IReadOnlyList<string> arguments,
        byte[] stdin,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var error = new StringBuilder();

        var command = Cli.Wrap(fileName)
            .WithArguments(arguments)
            .WithStandardInputPipe(stdin.Length == 0 ? PipeSource.Null : PipeSource.FromBytes(stdin))
            .WithStandardOutputPipe(PipeTarget.ToStream(output))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(error))
            .WithValidation(CommandResultValidation.None);

        if (environment is not null)
        {
            command = command.WithEnvironmentVariables(environment);
        }

        try
        {
            // Awaiting completion guarantees the stdin pipe has been fully drained, so the copy we
            // made from the caller's secret is no longer referenced once we reach the finally.
            var result = await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return new ProcessResult
            {
                ExitCode = result.ExitCode,
                StandardOutput = output.ToArray(),
                StandardError = error.ToString()
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stdin);
        }
    }
}
