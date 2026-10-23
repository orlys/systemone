/// <summary>Hugging Face 權重來源，掛在 <see cref="SystemOne.ModelSources"/> 上</summary>
public static class HuggingFaceModelSources
{
    /// <summary>從 Hugging Face 存放庫下載一次，之後命中快取</summary>
    /// <remarks>只有呼叫 <see cref="SystemOne.ModelSource.ResolveAsync"/> 才會連網</remarks>
    /// <param name="anchor">掛載點 <see cref="SystemOne.ModelSources"/></param>
    /// <param name="repository">例如 <c>convaiinnovations/laya</c></param>
    /// <param name="subfolder">存放庫內的子目錄</param>
    /// <param name="revision">分支、標籤或認可，預設 <c>main</c></param>
    /// <param name="cacheDirectory">快取根目錄；省略為 <c>%LOCALAPPDATA%/systemone/models</c></param>
    /// <param name="http">自訂的 <see cref="HttpClient"/>；省略時用共用的預設用戶端</param>
    /// <returns>來源</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> 不是 <c>name</c> 或 <c>owner/name</c>、<paramref name="revision"/> 為空白，
    /// 或 <paramref name="revision"/>、<paramref name="subfolder"/> 是絕對路徑或含 <c>..</c> 等會逃出快取目錄的片段
    /// </exception>
    public static SystemOne.ModelSource FromHuggingFace(
        this SystemOneModelSources anchor,
        string repository,
        string? subfolder = null,
        string revision = "main",
        string? cacheDirectory = null,
        HttpClient? http = null)
    {
        return new HuggingFaceModelSource(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            repository: repository,
            subfolder: subfolder,
            revision: revision,
            cacheDirectory: cacheDirectory);
    }
}