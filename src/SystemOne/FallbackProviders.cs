/// <summary>後援鏈的 <c>Use*</c> 進入點，掛在 <see cref="SystemOne.Providers"/> 上</summary>
public static class FallbackProviders
{
    /// <summary>依序嘗試每個提供者，丟 <see cref="SystemOneException"/> 就換下一個</summary>
    /// <remarks>順序決定資料可能流向哪個網路</remarks>
    /// <param name="anchor">掛載點 <see cref="SystemOne.Providers"/></param>
    /// <param name="providers">依優先順序排列的提供者</param>
    /// <returns>後援鏈</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">沒有任何提供者，或其中有 <see langword="null"/></exception>
    public static ISystemOne UseFallback(this SystemOneProviders anchor, params ISystemOne[] providers)
    {
        return new FallbackSystemOneProvider(providers);
    }
}