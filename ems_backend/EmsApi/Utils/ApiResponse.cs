namespace EmsApi.Utils;

/// <summary>統一回應格式 { success, message, data, status }</summary>
public class ApiResponse<T>
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public T? Data { get; set; }
	public int Status { get; set; }
}

public static class ApiResult
{
	public static ApiResponse<T> Ok<T>(T data, string msg = "成功") =>
		new() { Success = true, Message = msg, Data = data, Status = 200 };

	public static ApiResponse<object> Fail(int status, string msg) =>
		new() { Success = false, Message = msg, Data = null, Status = status };
}
