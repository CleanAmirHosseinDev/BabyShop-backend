using BabyShop.Application.Dtos;
using BabyShop.Core.Exceptions;

namespace BabyShop.API.Middleware;

/// <summary>
/// آخرین خط دفاعی: هر خطای پیش‌بینی‌نشده را لاگ می‌کند و به‌جای stack-trace خام،
/// یک پاسخ استاندارد و امن برمی‌گرداند.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        var (statusCode, isServerError, message) = Map(exception);

        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            if (isServerError)
            {
                _logger.LogError(exception,
                    "Unhandled exception for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path.Value);
            }
            else
            {
                _logger.LogWarning(exception,
                    "Handled {ExceptionType} for {Method} {Path}",
                    exception.GetType().Name,
                    context.Request.Method,
                    context.Request.Path.Value);
            }
        }

        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new ApiResponse<object>
        {
            Success = false,
            // در Production هیچ جزئیات داخلی یا stack-trace ارسال نمی‌شود.
            Message = message,
            Errors = new List<string> { $"TraceId: {correlationId}" }
        };

        if (_environment.IsDevelopment() && isServerError)
            body.Errors.Add(exception.ToString());

        await context.Response.WriteAsJsonAsync(body);
    }

    private static (int StatusCode, bool IsServerError, string Message) Map(Exception exception) =>
        exception switch
        {
            NotFoundException => (404, false, exception.Message),
            BusinessRuleException => (400, false, exception.Message),
            UnauthorizedAccessException => (401, false, "دسترسی غیرمجاز"),
            ArgumentException => (400, false, "درخواست نامعتبر است"),
            BadHttpRequestException => (400, false, "درخواست نامعتبر است"),
            _ => (500, true, "خطای سرور. لطفاً دوباره تلاش کنید.")
        };
}