/// <summary>Vercel AI Gateway 的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
/// <remarks>走 Vercel 的 TypeSafe 相容端點：純 /v1/systemone 加 Bearer，直接用 <see cref="HttpSystemOneProvider"/></remarks>
public static class VercelProviders
{
    /// <summary>Vercel AI Gateway</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="apiKey">AI Gateway 金鑰</param>
    /// <param name="model">例如 <c>typesafe-ai/jev</c></param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> 為空白</exception>
    public static ISystemOne UseVercelAiGateway(
        this SystemOneProviders anchor,
        string apiKey,
        string model,
        HttpClient? http = null)
    {
        // 環境變數沒設時常拿到 null：寧可在這裡失敗，也不把狀態以未驗證的請求送給第三方
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var endpoint = new SystemOneEndpoint
        {
            Uri = new Uri("https://ai-gateway.vercel.sh/typesafe/v1/systemone"),
            Locality = SystemOneEndpoint.LocalityKind.Hosted,
            DefaultModel = model,
            BearerToken = apiKey,
        };

        return new HttpSystemOneProvider(http ?? SystemOneProvider.DefaultHttpClient, endpoint);
    }
}