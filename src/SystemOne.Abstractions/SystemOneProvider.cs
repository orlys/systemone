/// <summary>
/// 所有 System One 提供者的基底：先驗證請求，再交給 <see cref="DecideCoreAsync"/>
/// </summary>
/// <remarks>自訂提供者只需覆寫 <see cref="DecideCoreAsync"/>；驗證與 <c>FallbackSystemOneProvider</c> 相容性由基底提供</remarks>
public abstract class SystemOneProvider : ISystemOne
{
    private static readonly HttpClient s_defaultHttp = CreateDefaultHttpClient();

    /// <summary>呼叫端沒有傳入 <see cref="HttpClient"/> 時，所有提供者與權重來源共用的預設用戶端</summary>
    /// <remarks>
    /// 以 <see cref="SocketsHttpHandler"/> 建立：連線兩分鐘回收（DNS 會更新）、逾時 60 秒；
    /// 所有提供者與權重來源共用，請勿修改它的屬性或預設標頭
    /// </remarks>
    public static HttpClient DefaultHttpClient
    {
        get
        {
            return s_defaultHttp;
        }
    }

    /// <inheritdoc/>
    public async Task<SystemOne.Response> DecideAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SystemOneValidation.EnsureValid(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await DecideCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>對已驗證的請求取得答案</summary>
    /// <param name="request">已通過 <see cref="SystemOne.Request"/> 驗證的請求</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>以題目名稱為鍵的答案</returns>
    /// <exception cref="SystemOneException">模型或傳輸層回報失敗</exception>
    /// <exception cref="OperationCanceledException">已取消</exception>
    protected abstract Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken);

    // 規則 080 的 stated 例外：零相依函式庫的唯一預設用戶端；處理常式定期回收所以 DNS 會更新，並有明確逾時
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
    }
}