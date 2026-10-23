using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Cloudflare Workers AI 上的 clef：帳號範圍的網址，回應包在 <c>{"success","result","errors"}</c> 信封裡</summary>
public sealed class CloudflareSystemOneProvider : HttpSystemOneProvider
{
    private readonly string _accountId;

    /// <summary>建立提供者</summary>
    /// <param name="http">送出請求的 <see cref="HttpClient"/></param>
    /// <param name="accountId">Cloudflare 帳號識別碼</param>
    /// <param name="apiToken">API 權杖</param>
    /// <param name="model">模型名，預設 <see cref="ClefProviders.ClefFlashModel"/></param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="http"/>、<paramref name="accountId"/> 或 <paramref name="model"/> 為 <see langword="null"/>
    /// </exception>
    internal CloudflareSystemOneProvider(
        HttpClient http,
        string accountId,
        string apiToken,
        string model = ClefProviders.ClefFlashModel)
        : base(http, CreateEndpoint(accountId, apiToken, model))
    {
        _accountId = accountId;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">任一參數為 <see langword="null"/></exception>
    protected override void PrepareRequest(SystemOne.Request request, JsonObject body, HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(message);
        base.PrepareRequest(request, body, message);

        if (body["model"]?.GetValue<string>() is { } model)
        {
            // Workers AI 的模型在網址路徑裡：本文的模型已依請求覆寫，網址要跟著換，否則會由別的模型回答
            message.RequestUri = RunUri(_accountId, model);
        }
    }

    /// <inheritdoc/>
    protected override SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)
    {
        var typeInfo = CloudflareEnvelope.JsonContext.Default.CloudflareEnvelope;

        if (JsonSerializer.Deserialize(body, typeInfo) is not { } envelope)
        {
            // 2xx 卻沒有本文
            throw new SystemOneException("Empty response body.", statusCode, body);
        }

        if (envelope.Success)
        {
            if (envelope.Result is { } result)
            {
                return result;
            }

            // 宣稱成功卻沒有結果，不能當成空答案
            throw new SystemOneException(
                message: "Cloudflare returned success=true without a result.",
                statusCode,
                responseBody: body);
        }

        // Workers AI 以 200 + success=false 回報模型錯誤
        var errors = envelope.Errors is { Count: > 0 } list
            ? string.Join("; ", list.Select(e => FormattableString.Invariant($"{e.Code}: {e.Message}")))
            : "Cloudflare returned success=false.";

        throw new SystemOneException(errors, statusCode, body);
    }

    private static Uri RunUri(string accountId, string model)
    {
        var account = Uri.EscapeDataString(accountId);
        var modelPath = Uri.EscapeDataString(model);

        return new Uri($"https://api.cloudflare.com/client/v4/accounts/{account}/ai/run/@cf/cloudflare/{modelPath}");
    }

    private static SystemOneEndpoint CreateEndpoint(string accountId, string apiToken, string model)
    {
        return new SystemOneEndpoint
        {
            Uri = RunUri(accountId, model),
            Locality = SystemOneEndpoint.LocalityKind.Hosted,
            DefaultModel = model,
            BearerToken = apiToken,
        };
    }
}