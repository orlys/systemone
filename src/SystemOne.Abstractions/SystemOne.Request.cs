using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>一次呼叫：狀態、具名的題目、選用的模型與影像</summary>
    /// <remarks>
    /// 從 <see cref="SystemOne.Requests"/> 開始串 <see cref="SystemOneRequestExtensions.Noul"/>、
    /// <see cref="SystemOneRequestExtensions.Image(SystemOne.Request, string, string)"/>、
    /// <see cref="SystemOneRequestExtensions.WithModel"/> 等方法，最後以 <c>.For(state)</c> 帶入狀態。
    /// 這個型別只承載資料；公開的 setter 是為了序列化
    /// </remarks>
    public sealed record Request
    {
        /// <summary>覆寫端點的預設模型</summary>
        [JsonPropertyName("model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Model { get; set; }

        /// <summary>字串、物件或陣列，題目所指涉的內容</summary>
        /// <remarks>還沒以 <c>.For(state)</c> 帶入時為 <see langword="null"/>，這時請求是範本，送出會被拒絕；提供者收到的請求一定有狀態</remarks>
        [JsonPropertyName("state")]
        public JsonNode State { get; set; } = default!;

        /// <summary>以名稱為鍵的題目</summary>
        /// <remarks>提供者拒絕沒有題目的請求</remarks>
        [JsonPropertyName("questions")]
        public required IReadOnlyDictionary<string, Question> Questions { get; set; } = default!;

        /// <summary>以預留位置名稱為鍵的影像</summary>
        /// <remarks>由 <c>HttpSystemOneProvider</c> 依傳輸格式輸出，不在此序列化</remarks>
        [JsonIgnore]
        public IReadOnlyDictionary<string, Image>? Images { get; set; }
    }
}