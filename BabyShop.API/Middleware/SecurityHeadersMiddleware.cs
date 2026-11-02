namespace BabyShop.API.Middleware;

/// <summary>
/// هدرهای امنیتی پایه روی تمام پاسخ‌ها.
/// توجه: Cross-Origin-Resource-Policy عمداً cross-origin است چون تصاویر محصولات
/// از دامنهٔ فرانت‌اند بارگذاری می‌شوند.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Resource-Policy"] = "cross-origin";

        // CSP فقط روی پاسخ‌های API اعمال می‌شود تا Swagger (dev) و فایل‌های استاتیک را خراب نکند.
        if (!_isDevelopment && context.Request.Path.StartsWithSegments("/api"))
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'self'";

        await _next(context);
    }
}