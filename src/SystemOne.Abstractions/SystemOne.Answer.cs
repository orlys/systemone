using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>一題的答案；以 <c>type</c> 區分四種題型</summary>
    /// <remarks>以 <see cref="SystemOne.Factory"/> 建立；公開的 setter 是為了序列化</remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(NoulAnswer), "noul")]
    [JsonDerivedType(typeof(ChoiceAnswer), "choice")]
    [JsonDerivedType(typeof(ScoreAnswer), "score")]
    [JsonDerivedType(typeof(BoxAnswer), "box")]
    public abstract record Answer
    {
        private protected Answer()
        {
        }
    }

    /// <summary>是非題的答案</summary>
    public sealed record NoulAnswer : Answer
    {
        /// <summary>「是」的機率，0–1</summary>
        [JsonPropertyName("noul")]
        public required double Probability { get; set; } = default!;
    }

    /// <summary>單選題的答案</summary>
    public sealed record ChoiceAnswer : Answer
    {
        /// <summary>被選中的選項名</summary>
        [JsonPropertyName("choice")]
        public required string Option { get; set; } = default!;

        /// <summary>被選中選項的機率</summary>
        [JsonPropertyName("confidence")]
        public required double Confidence { get; set; } = default!;

        /// <summary>每個選項的機率，鍵與題目的選項名一致</summary>
        [JsonPropertyName("probabilities")]
        public required IReadOnlyDictionary<string, double> Probabilities { get; set; } = default!;
    }

    /// <summary>刻度題的答案</summary>
    /// <remarks>
    /// <see cref="Legend"/> 與 <see cref="Probabilities"/> 的鍵是 "0"、"1"…，與傳輸格式一致
    /// </remarks>
    public sealed record ScoreAnswer : Answer
    {
        /// <summary>以機率加權的刻度索引（從 0 起算）</summary>
        [JsonPropertyName("score")]
        public required double Level { get; set; } = default!;

        /// <summary>最高機率刻度的機率</summary>
        [JsonPropertyName("confidence")]
        public required double Confidence { get; set; } = default!;

        /// <summary>索引 → 刻度名稱</summary>
        [JsonPropertyName("legend")]
        public required IReadOnlyDictionary<string, string> Legend { get; set; } = default!;

        /// <summary>索引 → 機率</summary>
        [JsonPropertyName("probabilities")]
        public required IReadOnlyDictionary<string, double> Probabilities { get; set; } = default!;
    }

    /// <summary>框選範圍</summary>
    /// <remarks>四邊皆為 0–1 的比例：上下以高度從頂端起算，左右以寬度從左端起算</remarks>
    public sealed record BoxCoordinates
    {
        /// <summary>上緣，佔高度的比例</summary>
        [JsonPropertyName("top")]
        public required double Top { get; set; } = default!;

        /// <summary>下緣，佔高度的比例</summary>
        [JsonPropertyName("bottom")]
        public required double Bottom { get; set; } = default!;

        /// <summary>左緣，佔寬度的比例</summary>
        [JsonPropertyName("left")]
        public required double Left { get; set; } = default!;

        /// <summary>右緣，佔寬度的比例</summary>
        [JsonPropertyName("right")]
        public required double Right { get; set; } = default!;
    }

    /// <summary>框選題的答案</summary>
    public sealed record BoxAnswer : Answer
    {
        /// <summary>框選範圍</summary>
        [JsonPropertyName("box")]
        public required BoxCoordinates Bounds { get; set; } = default!;

        /// <summary>信心</summary>
        /// <remarks>伺服器未提供時為 <see langword="null"/></remarks>
        [JsonPropertyName("confidence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Confidence { get; set; }
    }
}