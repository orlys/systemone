using System.Text.Json.Serialization;

// Workers AI 的回應信封；由 CloudflareSystemOneProvider.ReadResponse 解析
internal sealed partial record CloudflareEnvelope
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SystemOne.Response? Result { get; set; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CloudflareError>? Errors { get; set; }

    internal sealed record CloudflareError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }
    }

    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true)]
    [JsonSerializable(typeof(CloudflareEnvelope))]
    internal sealed partial class JsonContext : JsonSerializerContext;
}