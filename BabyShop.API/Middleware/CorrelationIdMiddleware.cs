using System.Diagnostics;

namespace BabyShop.API.Middleware;

/// <summary>
/// یک شناسهٔ یکتا برای هر درخواست می‌سازد (یا شناسهٔ کلاینت را می‌پذیرد)،
/// آن را به هدر پاسخ و تمام لاگ‌های همان درخواست اضافه می‌کند و
/// مدت زمان اجرای هر درخواست را به‌صورت ساخت‌یافته لاگ می‌کند.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // تمام لاگ‌هایی که در طول این درخواست زده می‌شوند این شناسه را دارند.
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["UserId"] = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null
        }))
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        // فقط شناسه‌های امن را از کلاینت قبول می‌کنیم تا امکان header injection نباشد.
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming))
        {
            var value = incoming.ToString();
            if (!string.IsNullOrWhiteSpace(value)
                && value.Length <= 64
                && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N")[..16];
    }
}