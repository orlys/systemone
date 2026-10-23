using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>一題；以 <c>type</c> 區分四種題型</summary>
    /// <remarks>
    /// 由請求鏈的 <see cref="SystemOneRequestExtensions.Noul"/>、
    /// <see cref="SystemOneRequestExtensions.Choice(SystemOne.Request, string, string, string[])"/>、
    /// <see cref="SystemOneRequestExtensions.Score"/>、<see cref="SystemOneRequestExtensions.Box"/> 建立；
    /// 公開的 setter 是為了序列化
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(NoulQuestion), "noul")]
    [JsonDerivedType(typeof(ChoiceQuestion), "choice")]
    [JsonDerivedType(typeof(ScoreQuestion), "score")]
    [JsonDerivedType(typeof(BoxQuestion), "box")]
    public abstract record Question
    {
        /// <summary>給模型的指示</summary>
        /// <remarks>省略時只靠題型與選項</remarks>
        [JsonPropertyName("instructions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Instructions { get; set; }

        private protected Question()
        {
        }
    }

    /// <summary>是非題，答案是「是」的機率</summary>
    public sealed record NoulQuestion : Question;

    /// <summary>單選題，1–255 個選項</summary>
    /// <remarks>鍵是選項名，值是選用的說明</remarks>
    public sealed record ChoiceQuestion : Question
    {
        /// <summary>選項名 → 說明</summary>
        /// <remarks>說明可為 <see langword="null"/></remarks>
        [JsonPropertyName("criteria")]
        public required IReadOnlyDictionary<string, string?> Criteria { get; set; } = default!;
    }

    /// <summary>有序刻度題，1–10 級</summary>
    /// <remarks>答案是以機率加權的索引（從 0 起算）</remarks>
    public sealed record ScoreQuestion : Question
    {
        /// <summary>由低到高的刻度名稱</summary>
        [JsonPropertyName("criteria")]
        public required IReadOnlyList<string> Criteria { get; set; } = default!;
    }

    /// <summary>在請求的影像中框出指示描述的區域</summary>
    /// <remarks>TypeSafe 限定；需要 <see cref="Request.Images"/></remarks>
    public sealed record BoxQuestion : Question;
}