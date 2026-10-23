/// <summary>零信任存取決策的請求範本，掛在 <see cref="SystemOne.Requests"/> 上</summary>
public static class ZeroTrustRequests
{
    // 政策只寫一次，是還沒帶入狀態的範本；每個存取請求以 .For(signals) 帶入訊號
    private static readonly SystemOne.Request s_policy = SystemOne.Requests
        .Choice(
            "action",
            "How should the endpoint handle this request?",
            options => options
                .Option("allow", "serve the request as asked")
                .Option("step_up_mfa", "require a fresh phishing-resistant MFA before serving")
                .Option("read_only", "serve but downgrade to read-only scope")
                .Option("deny", "refuse the request and log it")
                .Option("isolate_device", "refuse, revoke the session and quarantine the device"))
        .Score(
            "risk",
            "Overall risk of this request",
            "negligible",
            "low",
            "moderate",
            "high",
            "critical")
        .Noul("credential_abuse", "Are the credentials likely being used by someone other than their owner?")
        .Noul("device_compromised", "Is the device likely compromised or out of compliance?")
        .Noul("notify_soc", "Should the SOC be notified about this request?");

    /// <summary>以一次存取請求蒐集到的訊號，詢問端點該怎麼處理</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Requests"/></param>
    /// <param name="signals">身分、裝置、網路、行為與資源的訊號</param>
    /// <returns>action、risk 與三個旗標五題的請求</returns>
    public static SystemOne.Request AccessDecision(this SystemOneRequests anchor, JsonObject signals)
    {
        return s_policy.For(signals);
    }
}