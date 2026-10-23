using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>串接 <see cref="SystemOne.Request"/>：加題目、影像、指定模型，最後帶入狀態</summary>
/// <remarks>
/// 每一步回傳新的請求，原本的不變。請求本身只承載資料（規則 062）；從 <see cref="SystemOne.Requests"/> 開始串，
/// 以 <c>.For(state)</c> 結尾
/// </remarks>
public static class SystemOneRequestExtensions
{
    /// <summary>加一題是非題</summary>
    /// <param name="request">請求</param>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <returns>加了這一題的新請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public static SystemOne.Request Noul(this SystemOne.Request request, string name, string instructions)
    {
        return request.WithQuestion(name, new SystemOne.NoulQuestion { Instructions = instructions });
    }

    /// <summary>加一題單選題，以 lambda 列出選項與說明</summary>
    /// <param name="request">請求</param>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="options">列出選項，例如 <c>options => options.Option("a", "…").Option("b")</c></param>
    /// <returns>加了這一題的新請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="options"/> 為 <see langword="null"/>，
    /// 或 lambda 回傳 <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或選項名空白、重複</exception>
    public static SystemOne.Request Choice(
        this SystemOne.Request request,
        string name,
        string? instructions,
        Func<SystemOneChoiceOptions, SystemOneChoiceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options(new SystemOneChoiceOptions())
            ?? throw new ArgumentNullException(nameof(options), "The options lambda returned null.");

        return request.WithQuestion(
            name,
            question: new SystemOne.ChoiceQuestion
            {
                Instructions = instructions,
                Criteria = configured.ToCriteria(),
            });
    }

    /// <summary>加一題單選題，只有選項名</summary>
    /// <param name="request">請求</param>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="options">選項名</param>
    /// <returns>加了這一題的新請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="options"/> 為 <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或選項名空白、重複</exception>
    public static SystemOne.Request Choice(
        this SystemOne.Request request,
        string name,
        string? instructions,
        params string[] options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 與 lambda 多載走同一套檢查：選項名不可空白、不可重複
        var configured = new SystemOneChoiceOptions();

        foreach (var option in options)
        {
            configured = configured.Option(option);
        }

        return request.WithQuestion(
            name,
            question: new SystemOne.ChoiceQuestion
            {
                Instructions = instructions,
                Criteria = configured.ToCriteria(),
            });
    }

    /// <summary>加一題刻度題，由低到高</summary>
    /// <param name="request">請求</param>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">給模型的指示</param>
    /// <param name="levels">刻度名稱，由低到高</param>
    /// <returns>加了這一題的新請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="levels"/> 為 <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public static SystemOne.Request Score(
        this SystemOne.Request request,
        string name,
        string? instructions,
        params string[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        // 複製一份：呼叫端之後修改自己的陣列，不會改變已建立的題目
        return request.WithQuestion(
            name,
            question: new SystemOne.ScoreQuestion
            {
                Instructions = instructions,
                Criteria = [.. levels],
            });
    }

    /// <summary>加一題框選題</summary>
    /// <param name="request">請求</param>
    /// <param name="name">題目名稱</param>
    /// <param name="instructions">要框出的東西</param>
    /// <returns>加了這一題的新請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    public static SystemOne.Request Box(this SystemOne.Request request, string name, string? instructions)
    {
        return request.WithQuestion(name, new SystemOne.BoxQuestion { Instructions = instructions });
    }

    /// <summary>從影像檔加一張影像</summary>
    /// <remarks>格式取自副檔名；同名的影像會被取代</remarks>
    /// <param name="request">請求</param>
    /// <param name="name">預留位置名稱；TypeSafe 會把狀態或指示裡出現這個字串的地方換成影像</param>
    /// <param name="path">影像檔路徑</param>
    /// <returns>加了這張影像的新請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或路徑沒有副檔名</exception>
    /// <exception cref="IOException">讀檔失敗</exception>
    /// <exception cref="UnauthorizedAccessException">沒有讀檔權限</exception>
    public static SystemOne.Request Image(this SystemOne.Request request, string name, string path)
    {
        return request.WithImage(name, SystemOne.Factory.Image(path));
    }

    /// <summary>從位元組加一張影像</summary>
    /// <remarks>同名的影像會被取代</remarks>
    /// <param name="request">請求</param>
    /// <param name="name">預留位置名稱；TypeSafe 會把狀態或指示裡出現這個字串的地方換成影像</param>
    /// <param name="bytes">原始影像位元組</param>
    /// <param name="format">png、jpg、jpeg 或 webp</param>
    /// <returns>加了這張影像的新請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 或 <paramref name="format"/> 為空白</exception>
    public static SystemOne.Request Image(
        this SystemOne.Request request,
        string name,
        ReadOnlyMemory<byte> bytes,
        string format)
    {
        return request.WithImage(name, SystemOne.Factory.Image(bytes, format));
    }

    /// <summary>帶入這次呼叫的狀態，放在請求鏈的最後</summary>
    /// <remarks>
    /// 回傳新的請求，原本的不變，所以同一個範本可以帶入不同狀態重複使用；已有狀態時取代它。
    /// 狀態會複製一份，呼叫端之後修改自己的節點不會影響已建立的請求
    /// </remarks>
    /// <param name="request">請求或範本</param>
    /// <param name="state">字串、物件或陣列狀態</param>
    /// <returns>帶有這個狀態的新請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="state"/> 為 <see langword="null"/>
    /// </exception>
    public static SystemOne.Request For(this SystemOne.Request request, JsonNode state)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);

        return request with { State = state.DeepClone() };
    }

    /// <summary>以任何可序列化物件（匿名型別、POCO、字典）帶入這次呼叫的狀態，放在請求鏈的最後</summary>
    /// <remarks>回傳新的請求，原本的不變；已有狀態時取代它</remarks>
    /// <param name="request">請求或範本</param>
    /// <param name="state">狀態</param>
    /// <returns>帶有這個狀態的新請求</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="state"/> 為 <see langword="null"/>
    /// </exception>
    /// <exception cref="ArgumentException">物件序列化後是 JSON null</exception>
    /// <exception cref="NotSupportedException">物件含有無法序列化的成員</exception>
    /// <exception cref="JsonException">物件含有循環參考，或巢狀超過序列化的深度上限</exception>
    public static SystemOne.Request For(this SystemOne.Request request, object state)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);

        if (JsonSerializer.SerializeToNode(state) is not { } node)
        {
            // 例如傳入的物件自訂序列化成 null
            throw new ArgumentException("State serialized to JSON null.", nameof(state));
        }

        return request with { State = node };
    }

    /// <summary>改用指定的模型，覆寫端點的預設模型</summary>
    /// <param name="request">請求</param>
    /// <param name="model">模型名</param>
    /// <returns>指定了模型的新請求</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="model"/> 為空白</exception>
    public static SystemOne.Request WithModel(this SystemOne.Request request, string model)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return request with { Model = model };
    }

    private static SystemOne.Request WithQuestion(
        this SystemOne.Request request,
        string name,
        SystemOne.Question question)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var questions = new Dictionary<string, SystemOne.Question>(request.Questions)
        {
            [name] = question,
        };

        return request with { Questions = questions };
    }

    private static SystemOne.Request WithImage(this SystemOne.Request request, string name, SystemOne.Image image)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Dictionary<string, SystemOne.Image> images = request.Images is null ? [] : new(request.Images);
        images[name] = image;

        return request with { Images = images };
    }
}