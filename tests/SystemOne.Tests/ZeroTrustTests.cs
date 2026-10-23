namespace SystemOneTests;

using System.Text.Json.Nodes;

// 對 clef 跑零信任範例的政策與情境；模型每次的機率會些微浮動，只檢查可接受的處置與明顯的風險方向
public sealed class ZeroTrustTests
{
    private static async Task<SystemOne.Response> DecideAsync(JsonObject signals)
    {
        var url = Environment.GetEnvironmentVariable("SYSTEMONE_CLEF_URL") ??
            throw new InvalidOperationException("SYSTEMONE_CLEF_URL");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var clef = SystemOne.Providers.UseClef(new Uri(url), http);
        return await clef.DecideAsync(SystemOne.Requests.AccessDecision(signals));
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task AccessDecision_OfficeLaptop_AllowsWithLowRisk()
    {
        // Arrange
        var signals = ZeroTrustScenarios.OfficeLaptop;

        // Act
        var response = await DecideAsync(signals);

        // Assert
        Assert.Equal("allow", response.GetChoice("action").Option);
        Assert.InRange(response.GetScore("risk").Level, 0, 2);
        Assert.InRange(response.GetNoul("credential_abuse").Probability, 0, 0.3);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task AccessDecision_NightlyDeploy_Allows()
    {
        // Arrange
        var signals = ZeroTrustScenarios.NightlyDeploy;

        // Act
        var response = await DecideAsync(signals);

        // Assert
        Assert.Equal("allow", response.GetChoice("action").Option);
        Assert.InRange(response.GetNoul("credential_abuse").Probability, 0, 0.5);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task AccessDecision_NewLocationExport_StepsUp()
    {
        // Arrange
        var signals = ZeroTrustScenarios.NewLocationExport;

        string[] acceptable = ["step_up_mfa", "read_only"];

        // Act
        var response = await DecideAsync(signals);

        // Assert
        Assert.Contains(response.GetChoice("action").Option, acceptable);
        Assert.InRange(response.GetNoul("device_compromised").Probability, 0, 0.5);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task AccessDecision_ContractorByod_RestrictsAndFlagsDevice()
    {
        // Arrange
        var signals = ZeroTrustScenarios.ContractorByod;

        string[] acceptable = ["step_up_mfa", "read_only", "deny"];

        // Act
        var response = await DecideAsync(signals);

        // Assert
        Assert.Contains(response.GetChoice("action").Option, acceptable);
        Assert.InRange(response.GetNoul("device_compromised").Probability, 0.5, 1);
    }

    [EnvironmentFact("SYSTEMONE_CLEF_URL")]
    public async Task AccessDecision_TorAdminDelete_BlocksWithCriticalRisk()
    {
        // Arrange
        var signals = ZeroTrustScenarios.TorAdminDelete;

        string[] acceptable = ["deny", "isolate_device"];

        // Act
        var response = await DecideAsync(signals);

        // Assert
        Assert.Contains(response.GetChoice("action").Option, acceptable);
        Assert.InRange(response.GetScore("risk").Level, 3, 4);
        Assert.InRange(response.GetNoul("credential_abuse").Probability, 0.5, 1);
        Assert.InRange(response.GetNoul("notify_soc").Probability, 0.5, 1);
    }
}