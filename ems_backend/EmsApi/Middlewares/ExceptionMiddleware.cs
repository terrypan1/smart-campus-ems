using System.Security;
using EmsApi.Utils;

namespace EmsApi.Middlewares;

/// <summary>可預期的例外轉成標準回應；系統錯誤只記 log、回通用訊息，不把例外內容給前端。</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
	public async Task Invoke(HttpContext context)
	{
		try
		{
			await next(context);
		}
		catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
		{
			// 用戶端自己斷線，不必回應
		}
		catch (Exception ex)
		{
			var (status, message) = ex switch
			{
				ArgumentException => (400, ex.Message),
				KeyNotFoundException => (404, ex.Message),
				UnauthorizedAccessException => (401, ex.Message),
				SecurityException => (403, ex.Message),
				_ => (500, "系統錯誤，請稍後再試"),
			};

			if (status == 500) logger.LogError(ex, "[TraceId={TraceId}] {Method} {Path} 未處理的例外", context.TraceIdentifier, context.Request.Method, context.Request.Path);
			else logger.LogWarning("[TraceId={TraceId}] {Method} {Path} {Status}：{Message}", context.TraceIdentifier, context.Request.Method, context.Request.Path, status, ex.Message);

			if (context.Response.HasStarted) throw;
			context.Response.StatusCode = status;
			await context.Response.WriteAsJsonAsync(ApiResult.Fail(status, message));
		}
	}
}
