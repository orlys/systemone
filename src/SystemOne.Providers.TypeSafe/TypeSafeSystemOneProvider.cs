using System.Text.Json.Nodes;

/// <summary>TypeSafe 託管的 Jev：影像以 <c>assets</c> 送出，需要時加 danger-zone 標頭</summary>
/// <remarks>影像至少 512 px；<see cref="SystemOne.BoxQuestion"/> 只有 <c>jev-preview</c> 接受</remarks>
public sealed class TypeSafeSystemOneProvider : HttpSystemOneProvider
{
    /// <summary>建立提供者</summary>
    /// <param name="http">送出請求的 <see cref="HttpClient"/></param>
    /// <param name="apiKey">TypeSafe API 金鑰</param>
    /// <param name="model">模型名，預設 <see cref="TypeSafeProviders.JevLatestModel"/></param>
    /// <exception cref="ArgumentNullException"><paramref name="http"/> 為 <see langword="null"/></exception>
    internal TypeSafeSystemOneProvider(
        HttpClient http,
        string apiKey,
        string model = TypeSafeProviders.JevLatestModel)
        : base(http, CreateEndpoint(apiKey, model))
    {
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">任一參數為 <see langword="null"/></exception>
    protected override void PrepareRequest(SystemOne.Request request, JsonObject body, HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(message);

        if (request.Images is { Count: > 0 } images)
        {
            // 有影像：字典鍵是預留位置名稱，TypeSafe 把狀態或指示裡出現該字串的地方換成影像
            var assets = new JsonObject();

            foreach (var (key, image) in images)
            {
                assets[key] = new JsonObject
                {
                    ["type"] = "image",
                    ["format"] = image.Format,
                    ["data_base64"] = Convert.ToBase64String(image.Bytes.Span),
                };
            }

            body["assets"] = assets;
        }

        if ((request.Images is { Count: > 0 }) ||
            request.Questions.Values.Any(q => q is SystemOne.BoxQuestion))
        {
            // 有影像或框選題：TypeSafe 沒有這個標頭就拒收 assets 與 box 題
            message.Headers.Add("TypeSafe-Danger-Zone-Images", "true");
        }
    }

    private static SystemOneEndpoint CreateEndpoint(string apiKey, string model)
    {
        return new SystemOneEndpoint
        {
            Uri = new Uri("https://api.typesafe.ai/v1/systemone"),
            Locality = SystemOneEndpoint.LocalityKind.Hosted,
            DefaultModel = model,
            BearerToken = apiKey,
        };
    }
}