/// <summary>測試模擬提供者的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
public static class MockProviders
{
    /// <summary>逐題答案的模擬提供者：先全部給預設答案</summary>
    /// <remarks>
    /// 接著串 <see cref="MockSystemOneProvider.Noul"/>、<see cref="MockSystemOneProvider.Choice"/>、
    /// <see cref="MockSystemOneProvider.Score"/>、<see cref="MockSystemOneProvider.Box"/>，每一步回傳新的模擬提供者
    /// </remarks>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <returns>模擬提供者</returns>
    public static MockSystemOneProvider UseMock(this SystemOneProviders anchor)
    {
        return new MockSystemOneProvider();
    }

    /// <summary>每次呼叫都丟出 <paramref name="exception"/> 的模擬提供者</summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="exception">要丟的例外，例如 <see cref="SystemOneException"/></param>
    /// <returns>模擬提供者</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 為 <see langword="null"/></exception>
    public static MockSystemOneProvider UseMock(this SystemOneProviders anchor, Exception exception)
    {
        return new MockSystemOneProvider(exception);
    }

    /// <summary>完全自訂的模擬提供者：每個請求都交給 <paramref name="handler"/></summary>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="handler">收到請求時呼叫</param>
    /// <returns>模擬提供者</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 為 <see langword="null"/></exception>
    public static MockSystemOneProvider UseMock(
        this SystemOneProviders anchor,
        Func<SystemOne.Request, SystemOne.Response> handler)
    {
        return new MockSystemOneProvider(handler);
    }
}