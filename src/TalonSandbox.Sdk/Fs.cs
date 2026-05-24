using System.Net.Http.Headers;
using TalonSandbox.Internal;
using TalonSandbox.Models;

namespace TalonSandbox;

/// <summary>Filesystem operations inside a sandbox. Accessed via <see cref="Sandbox.Fs"/>.</summary>
public sealed class Fs
{
    private readonly string _sandboxId;
    private readonly SandboxClient _client;

    internal Fs(string sandboxId, SandboxClient client)
    {
        _sandboxId = sandboxId;
        _client = client;
    }

    /// <summary>Reads raw bytes from a file inside the sandbox.</summary>
    public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BuildFsPath(path));
        using var resp = await _client.SendRawAsync(req, cancellationToken).ConfigureAwait(false);
        return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a file as UTF-8 text.</summary>
    public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Writes raw bytes to a file (creates intermediate directories automatically).</summary>
    public async Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, BuildFsPath(path))
        {
            Content = new ByteArrayContent(data),
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes UTF-8 text to a file.</summary>
    public Task WriteTextAsync(string path, string text, CancellationToken cancellationToken = default)
        => WriteAsync(path, System.Text.Encoding.UTF8.GetBytes(text), cancellationToken);

    /// <summary>Lists entries in a directory.</summary>
    public async Task<IReadOnlyList<FsEntry>> ListAsync(
        string path, int offset = 0, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"{BuildListPath(path)}?offset={offset}&limit={limit}");
        var result = await _client.SendAsync(req, TalonJsonContext.Default.FsListResponse, cancellationToken)
            .ConfigureAwait(false);
        return result.Entries;
    }

    /// <summary>Removes a file or directory.</summary>
    public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, BuildFsPath(path));
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Build a fs path with each segment URL-encoded so filenames with
    /// spaces, Chinese characters, or shell-special chars don't break the
    /// HTTP request line or get sent verbatim as path-traversal attempts.
    /// </summary>
    private string BuildFsPath(string path)
        => $"/v1/sandboxes/{_sandboxId}/fs/{EncodePathSegments(path)}";

    private string BuildListPath(string path)
        => $"/v1/sandboxes/{_sandboxId}/fs-list/{EncodePathSegments(path)}";

    private static string EncodePathSegments(string path)
    {
        var trimmed = path.TrimStart('/');
        if (trimmed.Length == 0) return string.Empty;
        var parts = trimmed.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = Uri.EscapeDataString(parts[i]);
        }
        return string.Join('/', parts);
    }
}
