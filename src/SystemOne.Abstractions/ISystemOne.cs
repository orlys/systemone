/// <summary>System One 決策模型的提供者契約：送出一組題目，取回逐題答案</summary>
public interface ISystemOne
{
    /// <summary>對 <paramref name="request"/> 的狀態回答每一題</summary>
    /// <param name="request">狀態、題目與選用的模型、影像</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>以題目名稱為鍵的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">請求沒有狀態或題目，或選項、刻度、影像不合法</exception>
    /// <exception cref="SystemOneException">模型或傳輸層回報失敗</exception>
    /// <exception cref="OperationCanceledException">已取消</exception>
    Task<SystemOne.Response> DecideAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken = default);
}