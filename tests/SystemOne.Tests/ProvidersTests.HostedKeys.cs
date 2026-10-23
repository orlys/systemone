namespace SystemOneTests;

public sealed partial class ProvidersTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void HostedUse_BlankCredential_Throws(string? credential)
    {
        // Arrange：環境變數沒設時常拿到 null，不該把狀態以未驗證的請求送給第三方
        Action[] calls = [
            delegate { SystemOne.Providers.UseTypeSafeAi(credential!); },
            delegate { SystemOne.Providers.UseOpenRouter(credential!, "m"); },
            delegate { SystemOne.Providers.UseVercelAiGateway(credential!, "m"); },
            delegate { SystemOne.Providers.UseCloudflareWorkersAi(credential!, "token"); },
            delegate { SystemOne.Providers.UseCloudflareWorkersAi("account", credential!); }
        ];

        // Act & Assert
        foreach (var call in calls)
        {
            Assert.ThrowsAny<ArgumentException>(call);
        }
    }
}