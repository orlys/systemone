/// <summary>自架 HTTP 提供者的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
/// <remarks>
/// 託管服務在各自的套件：SystemOne.Providers.TypeSafe、SystemOne.Providers.Cloudflare、
/// SystemOne.Providers.OpenRouter、SystemOne.Providers.Vercel
/// </remarks>
public static class HttpProviders
{
    /// <summary>任何自架的 /v1/systemone 伺服器：clef-mlx、Kev、laya-serve、d3-mlx、Ollama</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="baseUri">伺服器位址</param>
    /// <param name="model">模型名</param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>提供者</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseUri"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseUri"/> 不是絕對位址</exception>
    public static ISystemOne UseSelfHosted(
        this SystemOneProviders anchor,
        Uri baseUri,
        string model,
        HttpClient? http = null)
    {
        return new HttpSystemOneProvider(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            endpoint: SystemOneEndpoint.SelfHosted(baseUri, model));
    }
}