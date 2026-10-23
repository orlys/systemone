using System.ComponentModel;
using System.Globalization;

/// <summary>
/// 離線用的 <see cref="ISystemOne"/>：逐題設定答案、每次失敗或完全自訂，並記錄每個請求
/// </summary>
/// <remarks>
/// 設定建好就不變：<see cref="Noul"/>、<see cref="Choice"/>、<see cref="Score"/>、<see cref="Box"/>、
/// <see cref="WithModel"/> 都回傳新的模擬提供者，原本的不受影響，跟請求鏈的語意一致。
/// 唯一會變的是各自的 <see cref="Requests"/>。沒設定的題目：noul 0.5；choice 第一個選項、機率均分；
/// score 中間刻度、機率均分；box 整張影像
/// </remarks>
public sealed class MockSystemOneProvider : SystemOneProvider
{
    private static readonly Dictionary<string, Func<string, SystemOne.Question, SystemOne.Answer>> s_noAnswers = [];

    private readonly Lock _gate = new();
    private readonly List<SystemOne.Request> _requests = [];
    private readonly IReadOnlyDictionary<string, Func<string, SystemOne.Question, SystemOne.Answer>> _answers;
    private readonly Func<SystemOne.Request, SystemOne.Response>? _handler;
    private readonly string _model;

    /// <summary>建立逐題答案的模擬提供者</summary>
    /// <remarks>先全部給預設答案，再串 <see cref="Noul"/> 等方法設定</remarks>
    internal MockSystemOneProvider()
        : this(model: "mock", answers: s_noAnswers, handler: null)
    {
    }

    /// <summary>建立每次呼叫都丟出指定例外的模擬提供者</summary>
    /// <param name="exception">要丟的例外，例如 <see cref="SystemOneException"/></param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 為 <see langword="null"/></exception>
    internal MockSystemOneProvider(Exception exception)
        : this(model: "mock", answers: s_noAnswers, handler: Throwing(exception))
    {
    }

    /// <summary>建立完全自訂的模擬提供者：每個請求都交給 <paramref name="handler"/></summary>
    /// <remarks>「第一次成功、之後失敗」這類依呼叫次數變化的行為寫在這裡</remarks>
    /// <param name="handler">收到請求時呼叫</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 為 <see langword="null"/></exception>
    internal MockSystemOneProvider(Func<SystemOne.Request, SystemOne.Response> handler)
        : this(model: "mock", answers: s_noAnswers, handler: Required(handler))
    {
    }

    private MockSystemOneProvider(
        string model,
        IReadOnlyDictionary<string, Func<string, SystemOne.Question, SystemOne.Answer>> answers,
        Func<SystemOne.Request, SystemOne.Response>? handler)
    {
        _model = model;
        _answers = answers;
        _handler = handler;
    }

    /// <summary>這個模擬提供者收到的每個請求，依序</summary>
    public IReadOnlyList<SystemOne.Request> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>回應裡的模型名改成 <paramref name="model"/></summary>
    /// <remarks>預設為 <c>mock</c>；請求有指定模型時以請求為準</remarks>
    /// <param name="model">模型名</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> 為空白</exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，模型名不會被用到</exception>
    public MockSystemOneProvider WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        EnsureAnswerMode();

        return new MockSystemOneProvider(model, _answers, handler: null);
    }

    /// <summary>這一題原樣回傳指定的答案，不看題型</summary>
    /// <remarks>
    /// 底層方法，IntelliSense 不顯示；一般請用 <see cref="Noul"/>、<see cref="Choice"/>、<see cref="Score"/>、
    /// <see cref="Box"/>，這裡給已經有完整答案物件的程式碼用
    /// </remarks>
    /// <param name="name">題目名稱</param>
    /// <param name="answer">答案</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> 為 <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，答案不會被用到</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public MockSystemOneProvider Answer(string name, SystemOne.Answer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        return With(
            name,
            answer: delegate
            {
                return answer;
            });
    }

    /// <summary>這一題（box）回傳指定的框選範圍</summary>
    /// <remarks>
    /// 四邊為 0 到 1 的比例。題型不符時，<see cref="ISystemOne.DecideAsync"/> 丟
    /// <see cref="InvalidOperationException"/>
    /// </remarks>
    /// <param name="name">題目名稱</param>
    /// <param name="top">上緣，佔高度的比例</param>
    /// <param name="bottom">下緣，佔高度的比例</param>
    /// <param name="left">左緣，佔寬度的比例</param>
    /// <param name="right">右緣，佔寬度的比例</param>
    /// <param name="confidence">信心；省略時答案不帶信心</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白，或上緣在下緣之下、左緣在右緣之右</exception>
    /// <exception cref="ArgumentOutOfRangeException">某一邊或信心不在 0 到 1 之間</exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，答案不會被用到</exception>
    public MockSystemOneProvider Box(
        string name,
        double top,
        double bottom,
        double left,
        double right,
        double? confidence = null)
    {
        var answer = SystemOne.Factory.Box(top, bottom, left, right, confidence);

        return With(
            name,
            answer: (questionName, question) =>
            {
                Expect<SystemOne.BoxQuestion>(questionName, question);

                return answer;
            });
    }

    /// <summary>這一題（noul）回傳指定機率</summary>
    /// <remarks>題型不符時，<see cref="ISystemOne.DecideAsync"/> 丟 <see cref="InvalidOperationException"/></remarks>
    /// <param name="name">題目名稱</param>
    /// <param name="probability">0 到 1 之間的機率</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> 不在 0 到 1 之間</exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，答案不會被用到</exception>
    public MockSystemOneProvider Noul(string name, double probability)
    {
        // 先建好答案：機率超出範圍時在設定當下就失敗，而不是等到收到請求
        var answer = SystemOne.Factory.Noul(probability);

        return With(
            name,
            answer: (questionName, question) =>
            {
                Expect<SystemOne.NoulQuestion>(questionName, question);

                return answer;
            });
    }

    /// <summary>這一題（choice）選指定選項</summary>
    /// <remarks>
    /// 機率依題目的選項展開，選中的 1、其餘 0。
    /// 題型不符或選項不在題目裡時，<see cref="ISystemOne.DecideAsync"/> 丟 <see cref="InvalidOperationException"/>
    /// </remarks>
    /// <param name="name">題目名稱</param>
    /// <param name="option">選項名</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> 為 <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，答案不會被用到</exception>
    public MockSystemOneProvider Choice(string name, string option)
    {
        ArgumentNullException.ThrowIfNull(option);

        return With(
            name,
            answer: (questionName, question) =>
            {
                var criteria = Expect<SystemOne.ChoiceQuestion>(questionName, question).Criteria;

                if (!criteria.ContainsKey(option))
                {
                    // 設定的選項不在這次請求的題目裡，回傳它只會讓測試驗到不可能的答案
                    throw new InvalidOperationException(
                        $"Mock answer '{option}' for '{questionName}' is not one of its options.");
                }

                var picked = criteria.Keys.Select(key =>
                    (key, string.Equals(key, option, StringComparison.Ordinal) ? 1.0 : 0.0));

                return SystemOne.Factory.Choice([.. picked]);
            });
    }

    /// <summary>這一題（score）回傳指定刻度</summary>
    /// <remarks>
    /// legend 與機率依題目的刻度展開，最接近的刻度為 1。
    /// 題型不符或分數超過最高刻度時，<see cref="ISystemOne.DecideAsync"/> 丟 <see cref="InvalidOperationException"/>
    /// </remarks>
    /// <param name="name">題目名稱</param>
    /// <param name="level">0 起算的刻度期望值</param>
    /// <returns>新的模擬提供者</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> 為空白</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 為負</exception>
    /// <exception cref="InvalidOperationException">這是每次失敗或完全自訂的模擬提供者，答案不會被用到</exception>
    public MockSystemOneProvider Score(string name, double level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);

        return With(
            name,
            answer: (questionName, question) =>
            {
                var scale = Expect<SystemOne.ScoreQuestion>(questionName, question);

                if (level > (scale.Criteria.Count - 1))
                {
                    // 分數超過這次請求的最高刻度
                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Mock score {level} for '{questionName}' exceeds its levels."));
                }

                var nearest = (int)Math.Round(level);

                return new SystemOne.ScoreAnswer
                {
                    Level = level,
                    Confidence = 1,
                    Legend = Legend(scale),
                    Probabilities = Levels(scale).ToDictionary(
                        keySelector: Key,
                        elementSelector: index => index == nearest ? 1.0 : 0.0),
                };
            });
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">設定的答案與這次請求的題目不符</exception>
    protected override Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _requests.Add(request);
        }

        if (_handler is { } handler)
        {
            return Task.FromResult(handler(request));
        }

        var response = SystemOne.Factory.Response(
            model: request.Model ?? _model,
            answers: [
                .. request.Questions.Select(question => (question.Key, AnswerFor(question.Key, question.Value)))
            ]);

        return Task.FromResult(response);
    }

    private static Func<SystemOne.Request, SystemOne.Response> Throwing(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return delegate
        {
            throw exception;
        };
    }

    private static Func<SystemOne.Request, SystemOne.Response> Required(
        Func<SystemOne.Request, SystemOne.Response> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return handler;
    }

    private MockSystemOneProvider With(string name, Func<string, SystemOne.Question, SystemOne.Answer> answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureAnswerMode();

        var answers = new Dictionary<string, Func<string, SystemOne.Question, SystemOne.Answer>>(_answers)
        {
            [name] = answer,
        };

        return new MockSystemOneProvider(_model, answers, handler: null);
    }

    private void EnsureAnswerMode()
    {
        if (_handler is not null)
        {
            // 每次失敗或完全自訂的模擬提供者不看逐題答案，設定了也不會生效
            throw new InvalidOperationException(
                "This mock always throws or uses its handler; per-question answers would be ignored.");
        }
    }

    private SystemOne.Answer AnswerFor(string name, SystemOne.Question question)
    {
        return _answers.TryGetValue(name, out var make) ? make(name, question) : DefaultAnswer(question);
    }

    private static SystemOne.Answer DefaultAnswer(SystemOne.Question question)
    {
        switch (question)
        {
            case SystemOne.NoulQuestion:
                {
                    return SystemOne.Factory.Noul(0.5);
                }

            case SystemOne.ChoiceQuestion { Criteria: var criteria }:
                {
                    // 機率均分，同分時工廠取第一個選項
                    var probability = 1.0 / criteria.Count;

                    return SystemOne.Factory.Choice([.. criteria.Keys.Select(key => (key, probability))]);
                }

            case SystemOne.ScoreQuestion { Criteria: var criteria } scale:
                {
                    var probability = 1.0 / criteria.Count;

                    return new SystemOne.ScoreAnswer
                    {
                        Level = criteria.Count / 2,
                        Confidence = probability,
                        Legend = Legend(scale),
                        Probabilities = Levels(scale).ToDictionary(
                            keySelector: Key,
                            elementSelector: delegate
                            {
                                return probability;
                            }),
                    };
                }

            case SystemOne.BoxQuestion:
                {
                    return SystemOne.Factory.Box(top: 0, bottom: 1, left: 0, right: 1);
                }

            default:
                {
                    // Question 的建構函式是 private protected，新題型只會來自這個函式庫本身
                    throw new ArgumentOutOfRangeException(
                        paramName: nameof(question),
                        actualValue: question.GetType().Name,
                        message: "Unknown question type.");
                }
        }
    }

    private static TQuestion Expect<TQuestion>(string name, SystemOne.Question question)
        where TQuestion : SystemOne.Question
    {
        if (question is not TQuestion typed)
        {
            // 設定的答案型別與這次請求的題型不符
            throw new InvalidOperationException(
                $"Mock answer for '{name}' expects {typeof(TQuestion).Name} " +
                $"but the request has {question.GetType().Name}.");
        }

        return typed;
    }

    private static int[] Levels(SystemOne.ScoreQuestion scale)
    {
        return [.. Enumerable.Range(0, scale.Criteria.Count)];
    }

    private static Dictionary<string, string> Legend(SystemOne.ScoreQuestion scale)
    {
        return Levels(scale).ToDictionary(keySelector: Key, elementSelector: level => scale.Criteria[level]);
    }

    private static string Key(int level)
    {
        return level.ToString(CultureInfo.InvariantCulture);
    }
}