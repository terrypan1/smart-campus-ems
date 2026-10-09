using System.Text.Json;
using EmsApi.Models;

namespace EmsApi.Services.Settings;

/// <summary>設定類修改寫 audit_logs：時間、帳號、項目、修改前、修改後（JSON）</summary>
public class AuditService(EmsDbContext db)
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>加進 DbContext，跟著呼叫端的 SaveChanges 一起寫入（同一個交易）</summary>
	public void Add(string account, string item, object? before, object? after) =>
		db.AuditLogs.Add(new AuditLog
		{
			At = DateTimeOffset.UtcNow,
			Account = account,
			Item = item,
			Before = before == null ? null : JsonSerializer.Serialize(before, Json),
			After = after == null ? null : JsonSerializer.Serialize(after, Json),
		});
}
