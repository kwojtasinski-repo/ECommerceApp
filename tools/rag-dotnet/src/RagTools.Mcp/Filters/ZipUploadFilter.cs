using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RagTools.Mcp.Filters;

/// <summary>
/// Validates a ZIP-upload endpoint before model binding runs:
///   * Content-Type must be <c>application/zip</c> or <c>application/octet-stream</c> → else 415
///   * Content-Length must not exceed <see cref="MaxBytes"/> (defaults to 50 MiB) → else 413
///
/// Both checks are stable, code-driven, and easy to unit-test — they live here so the
/// controller action stays focused on the success path. EmptyBody and InvalidZipArchive
/// are detected later by <c>IZipBatchParser</c> and mapped via
/// <c>BatchIngestOutcomeExtensions.ToActionResult</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ZipUploadFilter : Attribute, IAsyncActionFilter
{
    public const long DefaultMaxBytes = 50L * 1024 * 1024;

    public long MaxBytes { get; init; } = DefaultMaxBytes;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.HttpContext.Request;
        var contentType = request.ContentType ?? string.Empty;

        var typeOk =
            contentType.StartsWith("application/zip",          StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase);

        if (!typeOk)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Expected Content-Type application/zip, got '{contentType}'",
                code  = "UnsupportedContentType",
            })
            { StatusCode = 415 };
            return;
        }

        if (request.ContentLength is long len && len > MaxBytes)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Request body too large ({len:N0} bytes). Limit is {MaxBytes / (1024 * 1024)} MiB.",
                code  = "PayloadTooLarge",
            })
            { StatusCode = 413 };
            return;
        }

        await next();
    }
}
