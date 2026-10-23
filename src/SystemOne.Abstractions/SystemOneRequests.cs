/// <summary><see cref="SystemOne.Requests"/> 的型別：請求鏈從這裡開始，其他套件的請求範本也掛在這裡</summary>
/// <remarks>
/// 先串題目、影像或模型，最後以 <see cref="SystemOneRequestExtensions.For(SystemOne.Request, System.Text.Json.Nodes.JsonNode)"/>
/// 帶入狀態。還沒帶入狀態的請求就是範本，可以重複使用
/// </remarks>
public sealed class SystemOneRequests
{
    internal SystemOneRequests()
    {
    }

    private static SystemOne.Request Empty()
    {
        return new SystemOne.Request
        {
            Questions = new Dictionary<string, SystemOne.Question>(),
        };
    }

    /// <summary>以一題是非題開始</summary>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public SystemOne.Request Noul(string name, string instructions)
    {
        return Empty().Noul(name, instructions);
    }

    /// <summary>以一題單選題開始，以 lambda 列出選項與說明</summary>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="options">列出選項，例如 <c>options => options.Option("a", "…").Option("b")</c></param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> 為 <see langword="null"/>，或 lambda 回傳 <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或選項名空白、重複</exception>
    public SystemOne.Request Choice(
        string name,
        string? instructions,
        Func<SystemOneChoiceOptions, SystemOneChoiceOptions> options)
    {
        return Empty().Choice(name, instructions, options);
    }

    /// <summary>以一題單選題開始，只有選項名</summary>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="options">選項名</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或選項名空白、重複</exception>
    public SystemOne.Request Choice(string name, string? instructions, params string[] options)
    {
        return Empty().Choice(name, instructions, options);
    }

    /// <summary>以一題刻度題開始，刻度由低到高</summary>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="levels">刻度名稱，由低到高</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="levels"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public SystemOne.Request Score(string name, string? instructions, params string[] levels)
    {
        return Empty().Score(name, instructions, levels);
    }

    /// <summary>以一題框選題開始</summary>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">要框出的東西</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public SystemOne.Request Box(string name, string? instructions)
    {
        return Empty().Box(name, instructions);
    }

    /// <summary>以影像檔中的一張影像開始</summary>
    /// <remarks>格式取自副檔名</remarks>
    /// <param name="name">預留位置名稱；TypeSafe 會把狀態或指示裡出現這個字串的地方換成影像</param>
    /// <param name="path">影像檔路徑</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或路徑沒有副檔名</exception>
    /// <exception cref="IOException">讀檔失敗</exception>
    /// <exception cref="UnauthorizedAccessException">沒有讀檔權限</exception>
    public SystemOne.Request Image(string name, string path)
    {
        return Empty().Image(name, path);
    }

    /// <summary>以位元組中的一張影像開始</summary>
    /// <param name="name">預留位置名稱；TypeSafe 會把狀態或指示裡出現這個字串的地方換成影像</param>
    /// <param name="bytes">原始影像位元組</param>
    /// <param name="format">png、jpg、jpeg 或 webp</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 或 <paramref name="format"/> 為空白</exception>
    public SystemOne.Request Image(string name, ReadOnlyMemory<byte> bytes, string format)
    {
        return Empty().Image(name, bytes, format);
    }

    /// <summary>以指定的模型開始，覆寫端點的預設模型</summary>
    /// <param name="model">模型名</param>
    /// <returns>還沒有狀態的請求</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> 為空白</exception>
    public SystemOne.Request WithModel(string model)
    {
        return Empty().WithModel(model);
    }
}