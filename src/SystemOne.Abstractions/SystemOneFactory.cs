/// <summary><see cref="SystemOne.Factory"/> 的型別：建立答案、回應與影像</summary>
/// <remarks>呼叫端不必直接 <see langword="new"/>；其他套件可用 <c>this SystemOneFactory</c> 擴充方法加入自己的工廠</remarks>
public sealed class SystemOneFactory
{
    internal SystemOneFactory()
    {
    }

    /// <summary>是非題的答案</summary>
    /// <param name="probability">「是」的機率，0 到 1</param>
    /// <returns>答案</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> 不在 0 到 1 之間</exception>
    public SystemOne.NoulAnswer Noul(double probability)
    {
        EnsureFraction(probability, nameof(probability));

        return new SystemOne.NoulAnswer { Probability = probability };
    }

    /// <summary>單選題的答案：選機率最高的選項，信心取該機率</summary>
    /// <remarks>同分時取先出現的</remarks>
    /// <param name="probabilities">依選項順序排列的選項名與機率</param>
    /// <returns>答案；<see cref="SystemOne.ChoiceAnswer.Probabilities"/> 保留傳入順序</returns>
    /// <exception cref="ArgumentNullException"><paramref name="probabilities"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">沒有任何選項，或選項名重複</exception>
    /// <exception cref="ArgumentOutOfRangeException">某個機率不在 0 到 1 之間</exception>
    public SystemOne.ChoiceAnswer Choice(params (string Option, double Probability)[] probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);

        if (probabilities is not { Length: > 0 })
        {
            // 沒有選項就沒有可選的答案
            throw new ArgumentException("A choice answer needs at least one option.", nameof(probabilities));
        }

        var best = probabilities[0];

        foreach (var entry in probabilities)
        {
            EnsureFraction(entry.Probability, nameof(probabilities));

            if (entry.Probability > best.Probability)
            {
                best = entry;
            }
        }

        return new SystemOne.ChoiceAnswer
        {
            Option = best.Option,
            Confidence = best.Probability,
            Probabilities = probabilities.ToDictionary(
                keySelector: entry => entry.Option,
                elementSelector: entry => entry.Probability),
        };
    }

    /// <summary>框選題的答案</summary>
    /// <remarks>四邊為 0 到 1 的比例</remarks>
    /// <param name="top">上緣，佔高度的比例</param>
    /// <param name="bottom">下緣，佔高度的比例</param>
    /// <param name="left">左緣，佔寬度的比例</param>
    /// <param name="right">右緣，佔寬度的比例</param>
    /// <param name="confidence">信心；伺服器沒給時為 <see langword="null"/></param>
    /// <returns>答案</returns>
    /// <exception cref="ArgumentOutOfRangeException">某一邊或信心不在 0 到 1 之間</exception>
    /// <exception cref="ArgumentException">上緣在下緣之下，或左緣在右緣之右</exception>
    public SystemOne.BoxAnswer Box(
        double top,
        double bottom,
        double left,
        double right,
        double? confidence = null)
    {
        EnsureFraction(top, nameof(top));
        EnsureFraction(bottom, nameof(bottom));
        EnsureFraction(left, nameof(left));
        EnsureFraction(right, nameof(right));

        if (confidence is { } value)
        {
            EnsureFraction(value, nameof(confidence));
        }

        if ((top > bottom) || (left > right))
        {
            // 邊界顛倒的框沒有意義，下游裁切或畫框都會出錯
            throw new ArgumentException("Box edges are reversed: top must not exceed bottom, nor left exceed right.");
        }

        return new SystemOne.BoxAnswer
        {
            Bounds = new SystemOne.BoxCoordinates
            {
                Top = top,
                Bottom = bottom,
                Left = left,
                Right = right,
            },
            Confidence = confidence,
        };
    }

    /// <summary>一次回應：模型名與以題目名稱為鍵的答案，用量為 0</summary>
    /// <remarks>給自訂提供者與 <c>UseMock(request =&gt; ...)</c> 這類處理常式使用</remarks>
    /// <param name="model">回應的模型名</param>
    /// <param name="answers">題目名稱與答案</param>
    /// <returns>回應</returns>
    /// <exception cref="ArgumentNullException">任一參數為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">題目名稱重複</exception>
    public SystemOne.Response Response(string model, params (string Name, SystemOne.Answer Answer)[] answers)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(answers);

        return new SystemOne.Response
        {
            Model = model,
            Answers = answers.ToDictionary(keySelector: entry => entry.Name, elementSelector: entry => entry.Answer),
        };
    }

    /// <summary>從位元組建立影像</summary>
    /// <param name="bytes">原始影像位元組</param>
    /// <param name="format">png、jpg、jpeg 或 webp</param>
    /// <returns>影像</returns>
    /// <exception cref="ArgumentException"><paramref name="format"/> 為空白</exception>
    public SystemOne.Image Image(ReadOnlyMemory<byte> bytes, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return new SystemOne.Image { Bytes = bytes, Format = format };
    }

    /// <summary>讀取影像檔</summary>
    /// <remarks>格式取自副檔名（轉小寫）</remarks>
    /// <param name="path">影像檔路徑</param>
    /// <returns>影像</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> 為空白或沒有副檔名</exception>
    /// <exception cref="IOException">讀檔失敗</exception>
    /// <exception cref="UnauthorizedAccessException">沒有讀檔權限</exception>
    public SystemOne.Image Image(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Path.GetExtension(path).TrimStart('.') is not { Length: > 0 } extension)
        {
            // 沒有副檔名就無從得知影像格式
            throw new ArgumentException("Cannot infer the image format: the path has no extension.", nameof(path));
        }

        return Image(File.ReadAllBytes(path), extension.ToLowerInvariant());
    }

    private static void EnsureFraction(double value, string paramName)
    {
        if (value is < 0 or > 1 or double.NaN)
        {
            // 機率與框選比例都是 0 到 1，超出範圍送進答案只會讓下游算錯
            throw new ArgumentOutOfRangeException(
                paramName,
                actualValue: value,
                message: "Value must be between 0 and 1.");
        }
    }
}