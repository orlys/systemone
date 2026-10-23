using System.Net;

/// <summary>
/// 函式庫回報的所有失敗：HTTP 錯誤、無法解析的回應、缺少或型別不符的答案
/// </summary>
public sealed class SystemOneException : Exception
{
    /// <summary>建立例外</summary>
    /// <param name="message">錯誤訊息</param>
    /// <param name="statusCode">HTTP 狀態碼；非 HTTP 來源時省略</param>
    /// <param name="responseBody">伺服器回應的原始內容</param>
    /// <param name="innerException">根源例外</param>
    public SystemOneException(
        string message,
        HttpStatusCode? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>HTTP 狀態碼</summary>
    /// <remarks>錯誤不是來自 HTTP 回應時為 <see langword="null"/></remarks>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>伺服器回應的原始內容</summary>
    /// <remarks>沒有回應時為 <see langword="null"/></remarks>
    public string? ResponseBody { get; }
}