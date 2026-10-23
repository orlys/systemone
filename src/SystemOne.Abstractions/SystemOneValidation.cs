// 送出前的請求驗證；放在這裡而不是 record 上，讓請求與題目只承載資料（規則 062）
internal static class SystemOneValidation
{
    /// <summary>驗證請求內容</summary>
    /// <remarks>由 <see cref="SystemOneProvider"/> 在送出前呼叫</remarks>
    /// <param name="request">請求</param>
    /// <exception cref="ArgumentException">沒有狀態、沒有題目、題目內容不合法、影像沒有位元組或格式</exception>
    internal static void EnsureValid(SystemOne.Request request)
    {
        if (request.State is null)
        {
            // 還是範本：忘了在請求鏈最後帶入狀態
            throw new ArgumentException("Request has no state; end the chain with .For(state).", nameof(request));
        }

        if (request.Questions is not { Count: > 0 })
        {
            // 沒有題目，送出去只會被伺服器拒絕
            throw new ArgumentException("Request has no questions.", nameof(request));
        }

        foreach (var (name, question) in request.Questions)
        {
            EnsureValid(name, question);
        }

        if (request.Images is not { } images)
        {
            // 沒有影像，不用再檢查
            return;
        }

        foreach (var (name, image) in images)
        {
            if (image.Bytes.IsEmpty)
            {
                // 空影像對任何伺服器都沒有意義
                throw new ArgumentException($"Image '{name}' has no bytes.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(image.Format))
            {
                // 沒有格式就組不出 data URL 或 TypeSafe 資產
                throw new ArgumentException($"Image '{name}' has no format.", nameof(request));
            }
        }
    }

    private static void EnsureValid(string name, SystemOne.Question question)
    {
        switch (question)
        {
            case SystemOne.ChoiceQuestion { Criteria: not { Count: >= 1 and <= 255 } }:
                {
                    // 協定限制：1 到 255 個選項
                    throw new ArgumentException(
                        message: $"Choice question '{name}' needs 1-255 options.",
                        paramName: nameof(question));
                }

            case SystemOne.ScoreQuestion { Criteria: not { Count: >= 1 and <= 10 } }:
                {
                    // 協定限制：1 到 10 級刻度
                    throw new ArgumentException(
                        message: $"Score question '{name}' needs 1-10 levels.",
                        paramName: nameof(question));
                }
        }
    }
}