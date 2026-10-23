/// <summary>clef 的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
/// <remarks>clef 是模型不是服務：<see cref="UseClef"/> 只是替自架伺服器帶入模型名的 <see cref="HttpProviders.UseSelfHosted"/></remarks>
public static class ClefProviders
{
    /// <summary>clef-flash 的模型名</summary>
    public const string ClefFlashModel = "clef-flash";

    /// <summary>自架的 clef 伺服器（例如 clef-mlx），無驗證，模型 <see cref="ClefFlashModel"/></summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="baseUri">伺服器位址</param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>提供者</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseUri"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseUri"/> 不是絕對位址</exception>
    public static ISystemOne UseClef(this SystemOneProviders anchor, Uri baseUri, HttpClient? http = null)
    {
        return anchor.UseSelfHosted(baseUri, ClefFlashModel, http);
    }
}