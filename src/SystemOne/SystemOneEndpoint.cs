using System.Text;

/// <summary>
/// 描述一個 /v1/systemone 相容伺服器：網址、驗證與預設模型
/// </summary>
/// <remarks>廠商專屬的端點由各廠商套件建立（SystemOne.Providers.TypeSafe、SystemOne.Providers.Cloudflare 等）</remarks>
public sealed record SystemOneEndpoint
{
    /// <summary>資料是否離開本機網路</summary>
    /// <remarks>函式庫只記錄，不據此路由</remarks>
    public enum LocalityKind
    {
        /// <summary>自架，資料留在自己的網路</summary>
        SelfHosted,

        /// <summary>託管服務，資料送到第三方</summary>
        Hosted,
    }

    /// <summary>完整請求網址（含路徑）</summary>
    public required Uri Uri { get; set; } = default!;

    /// <summary>自架或託管</summary>
    public required LocalityKind Locality { get; set; } = default!;

    /// <summary>請求未指定模型時使用</summary>
    public string? DefaultModel { get; set; }

    /// <summary>設定時每個請求帶 <c>Authorization: Bearer</c></summary>
    public string? BearerToken { get; set; }

    /// <summary>任何自架的 /v1/systemone 伺服器：clef-mlx、Kev、laya-serve、d3-mlx、Ollama</summary>
    /// <remarks>會取代 <paramref name="baseUri"/> 的路徑</remarks>
    /// <param name="baseUri">伺服器位址</param>
    /// <param name="defaultModel">預設模型名</param>
    /// <returns>端點描述</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseUri"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseUri"/> 不是絕對位址</exception>
    internal static SystemOneEndpoint SelfHosted(Uri baseUri, string? defaultModel = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        return new SystemOneEndpoint
        {
            Uri = new Uri(baseUri, "/v1/systemone"),
            Locality = LocalityKind.SelfHosted,
            DefaultModel = defaultModel,
        };
    }

    // record 的 ToString 寫入記錄時不洩漏權杖
    private bool PrintMembers(StringBuilder builder)
    {
        var token = BearerToken is null ? "null" : "***";
        builder.Append(FormattableString.Invariant($"{nameof(Uri)} = {Uri}, "));
        builder.Append(FormattableString.Invariant($"{nameof(Locality)} = {Locality}, "));
        builder.Append(FormattableString.Invariant($"{nameof(DefaultModel)} = {DefaultModel}, "));
        builder.Append(FormattableString.Invariant($"{nameof(BearerToken)} = {token}"));

        return true;
    }
}