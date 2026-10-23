// 零信任的政策決策：一次存取請求蒐集到的訊號進去，端點該怎麼處理出來；跟其他範例用同一個 ISystemOne
using System.Globalization;

var clefUrl = Environment.GetEnvironmentVariable("SYSTEMONE_CLEF_URL") ?? "http://localhost:8000";
// 後援鏈：clef 連不上就由模擬提供者回答，範例在任何機器都能跑
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri(clefUrl)),
    SystemOne.Providers.UseMock());

string[] flags = ["credential_abuse", "device_compromised", "notify_soc"];
var culture = CultureInfo.CurrentCulture;

foreach (var (name, signals) in ZeroTrustScenarios.All)
{
    // 政策是掛在 SystemOne.Requests 上的範本（見 ZeroTrustRequests.cs），每個請求只換訊號
    var response = await systemOne.DecideAsync(SystemOne.Requests.AccessDecision(signals));
    var action = response.GetChoice("action");
    var risk = response.GetScore("risk");
    var level = ((int)Math.Round(risk.Level)).ToString(CultureInfo.InvariantCulture);
    Console.WriteLine($"== {name}  (answered by {response.Model})");
    Console.WriteLine(string.Create(culture, $"   action: {action.Option,-14} (confidence {action.Confidence:0.00})"));
    var top = risk.Legend.Count - 1;
    Console.WriteLine(string.Create(culture, $"   risk:   {risk.Level:0.0}/{top} ≈ {risk.Legend[level]}"));

    foreach (var flag in flags)
    {
        var probability = response.GetNoul(flag).Probability;
        Console.WriteLine(string.Create(culture, $"   {flag,-19} p={probability:0.00}"));
    }

    Console.WriteLine();
}