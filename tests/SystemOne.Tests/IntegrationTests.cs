namespace SystemOneTests;

using System.Globalization;
using System.Text.Json.Nodes;

public sealed class IntegrationTests
{
    // 1×1 透明 PNG（clef-mlx 收任何尺寸）
    private static readonly byte[] s_onePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAA" +
        "AABJRU5ErkJggg==");

    // 512×512 全黑 PNG：TypeSafe 實測 2026-10-21 拒絕 256px 以下（"Invalid image dimensions."）
    private static readonly byte[] s_black512Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAgAAAAIACAIAAAB7GkOtAAADEUlEQVR4nO3BgQAAAADDoPlTX+EAVQEA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMBvArQAAVkUTe8AAAAASUVORK5C" +
        "YII=");

    private static readonly SystemOne.Request s_purchase = SystemOne.Requests
        .Noul("big", "Is the amount larger than 10000 TWD?")
        .Choice(
            "cat",
            "Merchant category",
            options => options
                .Option("grocery", "supermarkets")
                .Option("convenience", "convenience stores like 7-Eleven"))
        .Score("risk", "Fraud risk", "low", "mid", "high")
        .For(new JsonObject
        {
            ["amount"] = 12000,
            ["currency"] = "TWD",
            ["merchant"] = "7-Eleven",
        });

    // 選項名與刻度都是中文：STJ 送出 \uXXXX 逸出，伺服器回 UTF-8 鍵，兩邊必須對得上
    private static readonly SystemOne.Request s_chinese = SystemOne.Requests
        .Choice("部門", "應由哪個部門處理？", options => options
            .Option("帳務", "發票、付款、退款")
            .Option("技術", "系統錯誤、故障")
            .Option("業務", "報價、合約"))
        .Score("急迫程度", "多急？", "不急", "一般", "緊急")
        .For(new { 主旨 = "統編打錯了，發票要重開", 內容 = "上個月的發票統編少一碼，會計說不能報帳，麻煩重開並寄到載具。" });

    private static HttpClient Http(int seconds)
    {
        return new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
    }

    private static string Variable(string name)
    {
        return Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException(name);
    }

    private static void AssertPurchaseAnswers(SystemOne.Response response)
    {
        Assert.InRange(response.Get<SystemOne.NoulAnswer>("big").Probability, 0, 1);
        var cat = response.Get<SystemOne.ChoiceAnswer>("cat");
        Assert.Contains(cat.Option, (string[])["grocery", "convenience"]);
        Assert.Equal(2, cat.Probabilities.Count);
        var risk = response.Get<SystemOne.ScoreAnswer>("risk");
        Assert.Equal(3, risk.Legend.Count);
        Assert.InRange(risk.Level, 0, 2);
        Assert.True(response.Usage.InputTokens > 0);
    }

    private static void AssertChineseRoundTrip(SystemOne.Response response)
    {
        var department = response.Get<SystemOne.ChoiceAnswer>("部門");
        Assert.Equal(["帳務", "技術", "業務"], department.Probabilities.Keys.Order());

        // clef-flash 0.95 / jev 1.00（2026-10-22 實測）
        Assert.Equal("帳務", department.Option);
        var urgency = response.Get<SystemOne.ScoreAnswer>("急迫程度");
        var legend = urgency.Legend
            .OrderBy(kv => int.Parse(kv.Key, CultureInfo.InvariantCulture))
            .Select(kv => kv.Value);
        Assert.Equal(["不急", "一般", "緊急"], legend);
        Assert.Equal(3, urgency.Probabilities.Count);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task Clef_ChineseOptionNames_RoundTrip()
    {
        // Arrange
        using var http = Http(120);
        var clef = new HttpSystemOneProvider(
            http,
            endpoint: SystemOneEndpoint.SelfHosted(new Uri(Variable("SYSTEMONE_CLEF_URL")), "clef-flash"));

        // Act
        var response = await clef.DecideAsync(s_chinese);

        // Assert
        AssertChineseRoundTrip(response);
    }

    [EnvironmentFact("TYPESAFE_AI_API_KEY")]
    public async Task TypeSafe_ChineseOptionNames_RoundTrip()
    {
        // Arrange
        using var http = Http(60);
        var jev = SystemOne.Providers.UseTypeSafeAi(Variable("TYPESAFE_AI_API_KEY"), http: http);

        // Act
        var response = await jev.DecideAsync(s_chinese);

        // Assert
        AssertChineseRoundTrip(response);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task Clef_HealthThreeQuestionsAndImage_Answers()
    {
        // Arrange
        var baseUri = new Uri(Variable("SYSTEMONE_CLEF_URL"));
        using var http = Http(120);
        var clef = new HttpSystemOneProvider(http, SystemOneEndpoint.SelfHosted(baseUri, "clef-flash"));
        var withImage = SystemOne.Requests.Noul("dark", "Is the image mostly dark?").For("photo attached") with
        {
            Images = new Dictionary<string, SystemOne.Image>
            {
                ["photo"] = SystemOne.Factory.Image(s_onePixelPng, "png"),
            },
        };

        // Act
        var health = await http.GetStringAsync(new Uri(baseUri, "/health"));
        var purchase = await clef.DecideAsync(s_purchase);
        var image = await clef.DecideAsync(withImage);

        // Assert
        Assert.Contains("\"ok\"", health, StringComparison.Ordinal);
        AssertPurchaseAnswers(purchase);
        Assert.InRange(image.Get<SystemOne.NoulAnswer>("dark").Probability, 0, 1);
    }

    [EnvironmentFact("TYPESAFE_AI_API_KEY")]
    public async Task TypeSafe_ThreeQuestions_Answers()
    {
        // Arrange
        using var http = Http(60);
        var jev = SystemOne.Providers.UseTypeSafeAi(Variable("TYPESAFE_AI_API_KEY"), http: http);

        // Act
        var response = await jev.DecideAsync(s_purchase);

        // Assert
        AssertPurchaseAnswers(response);
        Assert.StartsWith("jev-", response.Model, StringComparison.Ordinal);
    }

    [EnvironmentFact("TYPESAFE_AI_API_KEY")]
    public async Task TypeSafe_ImageAssetAndBoxQuestion_Answers()
    {
        // Arrange
        using var http = Http(60);

        // box 題實測只有 jev-preview 接受；jev-latest 回 400 "Invalid request."
        var jev = SystemOne.Providers.UseTypeSafeAi(
            apiKey: Variable("TYPESAFE_AI_API_KEY"),
            model: "jev-preview",
            http);

        // "photo" 是資產的預留位置名稱：TypeSafe 會把狀態或指示裡出現該字串的地方換成影像
        var request = SystemOne.Requests
            .Noul("dark", "Is the image mostly dark?")
            .Box("where", "the darkest region")
            .For("photo") with
        {
            Images = new Dictionary<string, SystemOne.Image>
            {
                ["photo"] = SystemOne.Factory.Image(s_black512Png, "png"),
            },
        };

        // Act
        var response = await jev.DecideAsync(request);

        // Assert
        Assert.True(response.Get<SystemOne.NoulAnswer>("dark").Probability > 0.5);
        var box = response.Get<SystemOne.BoxAnswer>("where").Bounds;
        Assert.InRange(box.Top, 0, 1);
        Assert.InRange(box.Right, 0, 1);
    }
}