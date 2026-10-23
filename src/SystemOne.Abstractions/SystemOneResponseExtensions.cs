using System.ComponentModel;

/// <summary>從 <see cref="SystemOne.Response"/> 讀取型別化的答案</summary>
/// <remarks>回應本身只承載資料（規則 062）</remarks>
public static class SystemOneResponseExtensions
{
    /// <summary>讀取指定型別的答案</summary>
    /// <remarks>
    /// 底層方法，IntelliSense 不顯示；一般請用 <see cref="GetNoul"/>、<see cref="GetChoice"/>、
    /// <see cref="GetScore"/>、<see cref="GetBox"/>，這裡給依型別參數處理答案的程式碼用
    /// </remarks>
    /// <typeparam name="TAnswer">預期的答案型別</typeparam>
    /// <param name="response">回應</param>
    /// <param name="name">題目名稱</param>
    /// <returns>該題的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> 為 <see langword="null"/></exception>
    /// <exception cref="SystemOneException">沒有該名稱的答案，或答案不是 <typeparamref name="TAnswer"/></exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TAnswer Get<TAnswer>(this SystemOne.Response response, string name)
        where TAnswer : SystemOne.Answer
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Answers.GetValueOrDefault(name) is not { } answer)
        {
            // 題目名稱打錯或伺服器沒回這一題：列出有的名稱方便除錯
            var available = string.Join(", ", response.Answers.Keys);

            throw new SystemOneException($"No answer named '{name}'. Available: {available}.");
        }

        if (answer is not TAnswer typed)
        {
            // 呼叫端預期的題型與實際答案不符
            throw new SystemOneException(
                $"Answer '{name}' is a {answer.GetType().Name}, not a {typeof(TAnswer).Name}.");
        }

        return typed;
    }

    /// <summary>讀取是非題的答案</summary>
    /// <param name="response">回應</param>
    /// <param name="name">題目名稱</param>
    /// <returns>該題的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> 為 <see langword="null"/></exception>
    /// <exception cref="SystemOneException">沒有該名稱的答案，或答案不是是非題</exception>
    public static SystemOne.NoulAnswer GetNoul(this SystemOne.Response response, string name)
    {
        return response.Get<SystemOne.NoulAnswer>(name);
    }

    /// <summary>讀取單選題的答案</summary>
    /// <param name="response">回應</param>
    /// <param name="name">題目名稱</param>
    /// <returns>該題的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> 為 <see langword="null"/></exception>
    /// <exception cref="SystemOneException">沒有該名稱的答案，或答案不是單選題</exception>
    public static SystemOne.ChoiceAnswer GetChoice(this SystemOne.Response response, string name)
    {
        return response.Get<SystemOne.ChoiceAnswer>(name);
    }

    /// <summary>讀取刻度題的答案</summary>
    /// <param name="response">回應</param>
    /// <param name="name">題目名稱</param>
    /// <returns>該題的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> 為 <see langword="null"/></exception>
    /// <exception cref="SystemOneException">沒有該名稱的答案，或答案不是刻度題</exception>
    public static SystemOne.ScoreAnswer GetScore(this SystemOne.Response response, string name)
    {
        return response.Get<SystemOne.ScoreAnswer>(name);
    }

    /// <summary>讀取框選題的答案</summary>
    /// <param name="response">回應</param>
    /// <param name="name">題目名稱</param>
    /// <returns>該題的答案</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> 為 <see langword="null"/></exception>
    /// <exception cref="SystemOneException">沒有該名稱的答案，或答案不是框選題</exception>
    public static SystemOne.BoxAnswer GetBox(this SystemOne.Response response, string name)
    {
        return response.Get<SystemOne.BoxAnswer>(name);
    }
}