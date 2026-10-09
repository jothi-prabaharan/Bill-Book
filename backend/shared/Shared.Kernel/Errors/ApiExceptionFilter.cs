using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Errors;

/// <summary>
/// MVC Exception filter for API controllers.
/// In Development, returns the exact exception error in the response body.
/// In other environments, returns the catalogued user-safe message and suppresses schema/stack details.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(
        IHostEnvironment environment,
        ILogger<ApiExceptionFilter> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        Exception exception = context.Exception;
        TranslatedError translated = DbExceptionTranslator.Translate(exception);
        ApiErrorDiagnostics diagnostics = DbExceptionTranslator.Describe(exception);

        bool isDev = _environment.IsDevelopment();
        string exactMessage = isDev
            ? DbExceptionTranslator.GetExactMessage(exception)
            : translated.Message;

        _logger.Log(
            translated.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception,
            "{Method} {Path} failed: {Code} ({SqlState}) -> {Status}.",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            translated.Code,
            diagnostics.SqlState ?? "none",
            translated.StatusCode);

        var body = new ApiErrorResponse
        {
            Code = translated.Code.ToString(),
            Message = exactMessage,
            Status = translated.StatusCode,
            TraceId = context.HttpContext.TraceIdentifier,
            IsTransient = translated.IsTransient,
            ExceptionType = isDev ? diagnostics.ExceptionType : null,
            Detail = isDev ? diagnostics.Detail : null,
            StackTrace = isDev ? diagnostics.StackTrace : null,
            Diagnostics = isDev ? diagnostics : null,
        };

        context.Result = new ObjectResult(body)
        {
            StatusCode = translated.StatusCode,
        };

        context.ExceptionHandled = true;
    }
}
