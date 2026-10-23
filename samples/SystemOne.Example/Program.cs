using System.Diagnostics;
using System.Globalization;

// 選一個提供者：換成 UseTypeSafeAi、UseCloudflareWorkersAi 等，其餘程式碼都不用動
// 後援鏈：clef 連不上（丟 SystemOneException）就由模擬提供者回答，範例在任何機器都能跑
var clefUrl = Environment.GetEnvironmentVariable("SYSTEMONE_CLEF_URL") ?? "http://localhost:8000";
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri(clefUrl)),
    SystemOne.Providers.UseMock());

var request = SystemOne.Requests
    .Noul("refund", "Does the customer explicitly ask for a refund?")
    .Choice(
        "department",
        "Which department should handle this?",
        options => options
            .Option("billing", "invoices, payments, refunds")
            .Option("technical", "bugs, outages, integration problems")
            .Option("sales", "pricing, new contracts"))
    .Score("urgency", "How urgent is this?", "not urgent", "soon", "blocking or hard deadline")
    .For(new
    {
        from = "user@acme.com",
        subject = "Duplicate charge on invoice #4411",
        body = "We were billed twice for March. Refund the duplicate today or we cancel our plan.",
    });

var clock = Stopwatch.StartNew();
var response = await systemOne.DecideAsync(request);
clock.Stop();

var refund = response.GetNoul("refund");
var department = response.GetChoice("department");
var urgency = response.GetScore("urgency");
var level = ((int)Math.Round(urgency.Level)).ToString(CultureInfo.InvariantCulture);
var culture = CultureInfo.CurrentCulture;
var top = urgency.Legend.Count - 1;

// response.Model 顯示實際回答的是誰：clef-flash 或 mock
Console.WriteLine(string.Create(culture, $"answered by {response.Model} in {clock.ElapsedMilliseconds} ms"));
Console.WriteLine(string.Create(culture, $"refund:     p={refund.Probability:0.00}"));
var confidence = department.Confidence;
Console.WriteLine(string.Create(culture, $"department: {department.Option} (confidence {confidence:0.00})"));
Console.WriteLine(string.Create(culture, $"urgency:    {urgency.Level:0.00} of 0..{top} = {urgency.Legend[level]}"));
Console.WriteLine(string.Create(culture, $"{response.Usage.InputTokens} input tokens"));