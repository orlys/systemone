using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>權杖用量</summary>
    /// <remarks>伺服器未回報時兩者皆為 0</remarks>
    public sealed record Usage
    {
        /// <summary>輸入權杖數</summary>
        [JsonPropertyName("input_tokens")]
        public long InputTokens { get; set; }

        /// <summary>輸出權杖數</summary>
        [JsonPropertyName("output_tokens")]
        public long OutputTokens { get; set; }
    }
}