using System.Text.Json;
using TalonSandbox.Exceptions;
using TalonSandbox.Models;

namespace TalonSandbox.Internal;

internal static class HttpHelper
{
    public static void ThrowIfError(int statusCode, string? body)
    {
        if (statusCode < 400) return;

        var msg = ExtractMessage(body);
        var rid = ExtractRequestId(body);

        SandboxException ex = statusCode switch
        {
            400             => new SandboxException(msg, 400) { RequestId = rid },
            401 or 403      => new AuthException(msg, statusCode) { RequestId = rid },
            404             => new NotFoundException(msg) { RequestId = rid },
            422             => new QuotaException(msg) { RequestId = rid },
            429             => new RateLimitException(msg) { RequestId = rid },
            >= 500          => new ServerException(msg, statusCode) { RequestId = rid },
            _               => new SandboxException(msg, statusCode) { RequestId = rid },
        };

        throw ex;
    }

    private static string ExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Unknown server error";
        try
        {
            var err = JsonSerializer.Deserialize(body, TalonJsonContext.Default.ErrorResponse);
            if (!string.IsNullOrEmpty(err?.Error)) return err.Error;
        }
        catch (JsonException) { }
        return body;
    }

    private static string? ExtractRequestId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var err = JsonSerializer.Deserialize(body, TalonJsonContext.Default.ErrorResponse);
            return err?.RequestId;
        }
        catch (JsonException) { return null; }
    }
}
