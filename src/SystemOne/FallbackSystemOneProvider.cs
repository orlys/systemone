/// <summary>依序嘗試每個提供者的後援鏈</summary>
/// <remarks>
/// 提供者丟 <see cref="SystemOneException"/> 就換下一個，其他例外（取消、參數錯誤）直接往外丟
/// </remarks>
public sealed class FallbackSystemOneProvider : SystemOneProvider
{
    private readonly IReadOnlyList<ISystemOne> _providers;

    /// <summary>建立後援鏈</summary>
    /// <param name="providers">依優先順序排列的提供者</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">沒有任何提供者，或其中有 <see langword="null"/></exception>
    internal FallbackSystemOneProvider(IReadOnlyList<ISystemOne> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (providers is not { Count: > 0 })
        {
            // 空的後援鏈沒有任何提供者能回答
            throw new ArgumentException("At least one provider is required.", nameof(providers));
        }

        if (providers.Any(provider => provider is null))
        {
            // null 會到呼叫時才以 NullReferenceException 中斷整條鏈
            throw new ArgumentException("Providers must not contain null.", nameof(providers));
        }

        // 複製一份：呼叫端之後修改自己的陣列，不會改變後援鏈的順序
        _providers = [.. providers];
    }

    /// <summary>依優先順序排列的提供者</summary>
    public IReadOnlyList<ISystemOne> Providers
    {
        get
        {
            return _providers;
        }
    }

    /// <inheritdoc/>
    protected override async Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        // 收集每個提供者的失敗，全部失敗時一併回報
        List<SystemOneException> failures = [];

        foreach (var provider in _providers)
        {
            // 依優先順序嘗試，第一個成功的回應直接回傳
            try
            {
                return await provider.DecideAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (SystemOneException exception)
            {
                // 這個提供者失敗：記下例外，改試下一個
                failures.Add(exception);
            }
        }

        // 全部失敗：把每個失敗的訊息併成一則，並以最後一個失敗作為內部例外
        var summary = string.Join(
            separator: "; ",
            values: failures.Select((e, i) => FormattableString.Invariant($"[{i}] {e.Message}")));

        throw new SystemOneException(
            message: FormattableString.Invariant($"All {failures.Count} providers failed: {summary}"),
            innerException: failures[^1]);
    }
}