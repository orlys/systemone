using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>一次呼叫的結果</summary>
    /// <remarks>由提供者回傳；自訂提供者或測試處理常式以 <c>SystemOne.Factory.Response(...)</c> 建立</remarks>
    public sealed record Response
    {
        /// <summary>實際回答的模型名稱</summary>
        [JsonPropertyName("model")]
        public required string Model { get; set; } = default!;

        private IReadOnlyDictionary<string, Answer> _answers = new Dictionary<string, Answer>();
        private Usage _usage = new Usage();

        /// <summary>以題目名稱為鍵的答案</summary>
        /// <remarks>伺服器送 <see langword="null"/> 時為空</remarks>
        [JsonPropertyName("answers")]
        public required IReadOnlyDictionary<string, Answer> Answers
        {
            get
            {
                return _answers;
            }

            set
            {
                _answers = value ?? new Dictionary<string, Answer>();
            }
        }

        /// <summary>權杖用量</summary>
        /// <remarks>伺服器省略或送 <see langword="null"/> 時為全 0</remarks>
        [JsonPropertyName("usage")]
        public Usage Usage
        {
            get
            {
                return _usage;
            }

            set
            {
                _usage = value ?? new Usage();
            }
        }
    }
}