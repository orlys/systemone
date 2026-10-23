using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>對任何 /v1/systemone 相容伺服器取得答案</summary>
/// <remarks>
/// 一個 <see cref="System.Net.Http.HttpClient"/> 可服務多個端點。
/// 廠商的特殊行為在子類別覆寫 <see cref="PrepareRequest"/> 與 <see cref="ReadResponse"/>，核心不認識任何廠商
/// </remarks>
public class HttpSystemOneProvider : SystemOneProvider
{
    private static readonly MediaTypeHeaderValue s_json = new("application/json");

    private readonly HttpClient _http;
    private readonly SystemOneEndpoint _endpoint;

    /// <summary>建立提供者</summary>
    /// <param name="http">送出請求的 <see cref="System.Net.Http.HttpClient"/>；驗證每請求加在標頭上，不動它的預設標頭</param>
    /// <param name="endpoint">目標伺服器</param>
    /// <exception cref="ArgumentNullException">任一參數為 <see langword="null"/></exception>
    public HttpSystemOneProvider(HttpClient http, SystemOneEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(endpoint);
        _http = http;

        // 端點是可變的 record：存一份複本，呼叫端之後修改原物件不會把請求導到別處
        _endpoint = endpoint with { };
    }

    /// <summary>目標伺服器</summary>
    /// <remarks>回傳複本；修改它不會影響這個提供者</remarks>
    public SystemOneEndpoint Endpoint
    {
        get
        {
            return _endpoint with { };
        }
    }

    /// <summary>送出請求的 <see cref="System.Net.Http.HttpClient"/></summary>
    public HttpClient HttpClient
    {
        get
        {
            return _http;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">請求與端點都沒有模型名</exception>
    protected override async Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        if ((request.Model ?? _endpoint.DefaultModel) is not { } model)
        {
            // 請求與端點都沒有模型名，送出去只會被拒
            throw new ArgumentException(
                message: "Request has no Model and the endpoint has no DefaultModel.",
                paramName: nameof(request));
        }

        // 組請求：驗證、本文，再讓子類別加上廠商的影像形狀與標頭
        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint.Uri);

        if (_endpoint.BearerToken is { } token)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var body = Serialize(request with { Model = model });
        PrepareRequest(request, body, message);
        message.Content = new StringContent(body.ToJsonString(), s_json);

        // 送出：傳輸層失敗包成 SystemOneException，取消照常往外丟
        var response = default(HttpResponseMessage);

        try
        {
            response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // 連不上伺服器（DNS、連線、TLS）：當成伺服器失敗，讓後援鏈能換下一個
            throw new SystemOneException(
                message: $"Request to {_endpoint.Uri} failed: {exception.Message}",
                innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 不是呼叫端取消，而是 HttpClient.Timeout 到了：當成伺服器失敗，讓後援鏈能換下一個
            throw new SystemOneException(
                message: FormattableString.Invariant($"Request to {_endpoint.Uri} timed out after {_http.Timeout}."),
                innerException: exception);
        }

        using var _ = response;
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 伺服器拒絕：盡量取出人看得懂的訊息，原始本文一併帶上
            throw new SystemOneException(ExtractMessage(text), response.StatusCode, text);
        }

        // 解析：交給子類別拆廠商信封；解析中的任何失敗都是伺服器的錯，包成 SystemOneException 讓後援鏈換下一個
        try
        {
            return ReadResponse(text, response.StatusCode);
        }
        catch (Exception exception) when (exception is not SystemOneException)
        {
            // 本文不是預期的形狀，解析時丟出了其他例外
            throw new SystemOneException(
                message: $"Cannot parse response: {exception.Message}",
                statusCode: response.StatusCode,
                responseBody: text,
                innerException: exception);
        }
    }

    /// <summary>送出前調整本文與標頭</summary>
    /// <remarks>
    /// 預設把影像以 <c>images</c> data URL 陣列送出（clef 擴充）。覆寫時不必呼叫基底；
    /// 本文已含 <c>model</c>、<c>state</c>、<c>questions</c>
    /// </remarks>
    /// <param name="request">原始請求（影像在 <see cref="SystemOne.Request.Images"/>）</param>
    /// <param name="body">即將送出的 JSON 本文</param>
    /// <param name="message">即將送出的 HTTP 請求，已帶驗證標頭</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 或 <paramref name="body"/> 為 <see langword="null"/>
    /// </exception>
    protected virtual void PrepareRequest(SystemOne.Request request, JsonObject body, HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);

        if (request.Images is not { Count: > 0 } images)
        {
            // 沒有影像，本文原樣送出
            return;
        }

        body["images"] = new JsonArray(images.Values.Select(i => (JsonNode)ToDataUrl(i)).ToArray());
    }

    /// <summary>把 2xx 本文轉成答案</summary>
    /// <remarks>
    /// 預設直接解析 /v1/systemone 回應。這裡丟出的 <see cref="SystemOneException"/> 以外的例外，
    /// 都由呼叫端包成 <see cref="SystemOneException"/>
    /// </remarks>
    /// <param name="body">回應本文</param>
    /// <param name="statusCode">回應狀態碼，放進例外供診斷</param>
    /// <returns>答案</returns>
    /// <exception cref="SystemOneException">本文為空、模型名或答案為 <see langword="null"/>，或廠商回報失敗</exception>
    /// <exception cref="JsonException">本文不是合法的回應 JSON</exception>
    /// <exception cref="NotSupportedException">答案沒有 <c>type</c>，或其值無法辨識</exception>
    protected virtual SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)
    {
        if (JsonSerializer.Deserialize(body, SystemOne.JsonContext.Default.Response) is not { } response)
        {
            // 本文是 JSON null，沒有回應可讀
            throw new SystemOneException("Empty response body.", statusCode, body);
        }

        if ((response.Model is null) || response.Answers.Values.Any(answer => answer is null))
        {
            // JSON 的 null 會繞過 required；放行的話呼叫端會拿到 null 模型名或找不到答案的錯誤訊息
            throw new SystemOneException(
                message: "Response has a null model or a null answer.",
                statusCode,
                responseBody: body);
        }

        return response;
    }

    private static JsonObject Serialize(SystemOne.Request request)
    {
        // 影像依廠商而異，所以由 PrepareRequest 加在序列化後的節點上，不放在 record 裡
        if (JsonSerializer.SerializeToNode(request, SystemOne.JsonContext.Default.Request) is not JsonObject node)
        {
            // 序列化結果不是 JSON 物件，加不上廠商的欄位
            throw new SystemOneException("Request serialized to JSON null.");
        }

        return node;
    }

    private static string ToDataUrl(SystemOne.Image image)
    {
        var mediaType = string.Equals(image.Format, "jpg", StringComparison.OrdinalIgnoreCase)
            ? "jpeg"
            : image.Format.ToLowerInvariant();

        return $"data:image/{mediaType};base64,{Convert.ToBase64String(image.Bytes.Span)}";
    }

    // 依序嘗試：{"error":"text"}（Ollama / llama-server）→ {"error":{"message"}}（clef-mlx、OpenRouter）
    // → {"message"}（Vercel）→ {"detail":{"message"}}（TypeSafe 4xx）→ {"detail":[{"msg"}]}（TypeSafe 422）
    // → {"errors":[{"message"}]}（Cloudflare）→ 原始本文
    internal static string ExtractMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                // 不是 JSON 物件，沒有欄位可取
                return body;
            }

            if (Text(root, "error") is { } plain)
            {
                return plain;
            }

            if (Text(root, "error", "message") is { } nested)
            {
                return nested;
            }

            if (Text(root, "message") is { } top)
            {
                return top;
            }

            if (root.TryGetProperty("detail", out var detail))
            {
                if (Text(detail, "message") is { } detailMessage)
                {
                    return detailMessage;
                }

                if ((FirstElement(detail) is { } first) && (Text(first, "msg") is { } validation))
                {
                    return validation;
                }
            }

            if (root.TryGetProperty("errors", out var errors) &&
                (FirstElement(errors) is { } firstError) &&
                (Text(firstError, "message") is { } cloudflare))
            {
                return cloudflare;
            }
        }
        catch (JsonException)
        {
            // 不是 JSON：下面直接回原始本文
        }

        return body;

        static JsonElement? FirstElement(JsonElement element)
        {
            if ((element.ValueKind is not JsonValueKind.Array) || (element.GetArrayLength() is 0))
            {
                // 不是陣列或是空陣列，沒有第一個元素
                return null;
            }

            return element[0];
        }

        static string? Text(JsonElement element, params string[] path)
        {
            foreach (var segment in path)
            {
                if ((element.ValueKind is not JsonValueKind.Object) ||
                    !element.TryGetProperty(segment, out element))
                {
                    // 路徑上有一段不是物件，或沒有這個欄位
                    return null;
                }
            }

            return element.ValueKind is JsonValueKind.String ? element.GetString() : null;
        }
    }
}