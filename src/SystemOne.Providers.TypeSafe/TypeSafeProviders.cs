/// <summary>TypeSafe 的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
public static class TypeSafeProviders
{
    /// <summary>預設模型名</summary>
    public const string JevLatestModel = "jev-latest";

    /// <summary>TypeSafe 託管的 Jev</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="apiKey">TypeSafe API 金鑰</param>
    /// <param name="model">模型名，預設 <see cref="JevLatestModel"/></param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> 為空白</exception>
    public static ISystemOne UseTypeSafeAi(
        this SystemOneProviders anchor,
        string apiKey,
        string model = JevLatestModel,
        HttpClient? http = null)
    {
        // 環境變數沒設時常拿到 null：寧可在這裡失敗，也不把狀態以未驗證的請求送給第三方
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new TypeSafeSystemOneProvider(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            apiKey,
            model);
    }
}