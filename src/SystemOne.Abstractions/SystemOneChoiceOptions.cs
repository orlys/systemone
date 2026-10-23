/// <summary>單選題的選項清單</summary>
/// <remarks>
/// 每一步回傳新的清單，原本的不變。只在請求鏈 <c>.Choice(name, instructions, options => ...)</c> 的
/// lambda 裡取得；選項依加入順序送出
/// </remarks>
public sealed class SystemOneChoiceOptions
{
    private readonly IReadOnlyList<KeyValuePair<string, string?>> _options;

    internal SystemOneChoiceOptions()
        : this([])
    {
    }

    private SystemOneChoiceOptions(IReadOnlyList<KeyValuePair<string, string?>> options)
    {
        _options = options;
    }

    /// <summary>加一個選項</summary>
    /// <param name="name">選項名；回答時以它表示被選中的選項</param>
    /// <param name="description">給模型的說明</param>
    /// <returns>加了這個選項的新清單</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或與既有選項重複</exception>
    public SystemOneChoiceOptions Option(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var option in _options)
        {
            if (string.Equals(option.Key, name, StringComparison.Ordinal))
            {
                // 同名選項會讓答案的機率對不回選項
                throw new ArgumentException(
                    message: $"Option '{name}' is already defined.",
                    paramName: nameof(name));
            }
        }

        return new SystemOneChoiceOptions([.. _options, new(name, description)]);
    }

    internal Dictionary<string, string?> ToCriteria()
    {
        return new Dictionary<string, string?>(_options, StringComparer.Ordinal);
    }
}