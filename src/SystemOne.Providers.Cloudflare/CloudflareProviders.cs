/// <summary>Cloudflare Workers AI 的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
public static class CloudflareProviders
{
    /// <summary>Cloudflare Workers AI 上的 clef</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="accountId">Cloudflare 帳號識別碼</param>
    /// <param name="apiToken">API 權杖</param>
    /// <param name="model">模型名，預設 <see cref="ClefProviders.ClefFlashModel"/></param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="accountId"/> 或 <paramref name="apiToken"/> 為空白</exception>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> 為 <see langword="null"/></exception>
    public static ISystemOne UseCloudflareWorkersAi(
        this SystemOneProviders anchor,
        string accountId,
        string apiToken,
        string model = ClefProviders.ClefFlashModel,
        HttpClient? http = null)
    {
        // 環境變數沒設時常拿到 null：寧可在這裡失敗，也不把狀態以未驗證的請求送給第三方
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);

        return new CloudflareSystemOneProvider(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            accountId: accountId,
            apiToken: apiToken,
            model: model);
    }
}