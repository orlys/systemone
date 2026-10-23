using System.Text.Json.Serialization;

public static partial class SystemOne
{
    /// <summary>請求與回應的序列化內容，由來源產生器產生</summary>
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true)]
    [JsonSerializable(typeof(Request))]
    [JsonSerializable(typeof(Response))]
    internal sealed partial class JsonContext : JsonSerializerContext;
}