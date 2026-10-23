namespace SystemOneTests;

// 原始 JSON 以原始字串常值換行排版；JSON 允許 token 之間的空白，內容不變
internal static class Fixtures
{
    // clef-flash（clef-mlx，自架）實測：三題一次
    public const string ClefMulti = """
        {"model": "clef-flash",
         "answers": {
           "big": {"type": "noul", "noul": 0.7931},
           "cat": {"type": "choice", "choice": "convenience", "confidence": 0.966,
                   "probabilities": {"grocery": 0.034, "convenience": 0.966}},
           "risk": {"type": "score", "score": 1.1739, "confidence": 0.442,
                    "legend": {"0": "1", "1": "2", "2": "3"},
                    "probabilities": {"0": 0.2681, "1": 0.2899, "2": 0.442}}},
         "usage": {"input_tokens": 305, "output_tokens": 0, "latency_ms": 368.7}}
        """;

    // TypeSafe Jev 實測：同形狀，model 版本化，output_tokens 非 0
    public const string TypeSafeMulti = """
        {"model":"jev-1.13.0",
         "answers":{
           "refund":{"type":"noul","noul":0.98},
           "team":{"type":"choice","choice":"billing","confidence":1.0,
                   "probabilities":{"billing":1.0,"shipping":0.0}},
           "urgency":{"type":"score","score":1.1,"confidence":0.67,
                      "legend":{"0":"can wait","1":"this week","2":"today"},
                      "probabilities":{"0":0.06,"1":0.78,"2":0.16}}},
         "usage":{"input_tokens":388,"output_tokens":62}}
        """;

    // TypeSafe box 答案（依 docs.typesafe.ai/api 手寫；box 路徑已在整合測試實測）
    public const string TypeSafeBox = """
        {"model":"jev-preview",
         "answers":{"where":{"type":"box","box":{"top":0.1,"bottom":0.6,"left":0.2,"right":0.7},
                             "confidence":null}},
         "usage":{"input_tokens":1,"output_tokens":0}}
        """;

    // 同 ClefMulti，但 "type" 不是第一個屬性
    public const string TypeNotFirst = """
        {"model": "clef-flash", "answers": {"big": {"noul": 0.5, "type": "noul"}},
         "usage": {"input_tokens": 1, "output_tokens": 0}}
        """;

    // 未知答案型別
    public const string UnknownAnswerType = """
        {"model": "m", "answers": {"x": {"type": "polygon", "points": []}},
         "usage": {"input_tokens": 1, "output_tokens": 0}}
        """;

    // 缺 type 區分欄位
    public const string MissingAnswerType = """
        {"model": "m", "answers": {"x": {"noul": 0.5}}, "usage": {"input_tokens": 1, "output_tokens": 0}}
        """;

    // Vercel 風格：多 provider_metadata
    public const string WithProviderMetadata = """
        {"model":"typesafe-ai/jev","answers":{"refund":{"type":"noul","noul":0.98}},
         "usage":{"input_tokens":275,"output_tokens":20},
         "provider_metadata":{"gateway":{"cost":"0.00001155"}}}
        """;

    // pydantic 預設把選填欄位輸出成 null
    public const string UsageNull = """{"model":"m","answers":{"big":{"type":"noul","noul":0.5}},"usage":null}""";

    // answers 是 null
    public const string AnswersNull = """{"model":"m","answers":null,"usage":{"input_tokens":1,"output_tokens":0}}""";

    // 沒有 usage 欄位的回應（部分自架伺服器）
    public const string WithoutUsage = """{"model":"m","answers":{"big":{"type":"noul","noul":0.5}}}""";

    // 錯誤格式：clef-mlx / OpenRouter
    public const string ErrorClefMlx = """{"error": {"message": "q: type must be noul, choice, or score"}}""";

    // 錯誤格式：Vercel AI Gateway
    public const string ErrorVercelMessage = "questions.refund.type: expected one of 'noul', 'choice', 'score'";

    public const string ErrorVercel = """
        {"message": "questions.refund.type: expected one of 'noul', 'choice', 'score'",
         "error_type": "invalid_request"}
        """;

    // 錯誤格式：TypeSafe 422（FastAPI 驗證）
    public const string ErrorTypeSafe422 = """
        {"detail":[{"type":"missing","loc":["body","state"],"msg":"Field required"}]}
        """;

    // 錯誤格式：TypeSafe 400
    public const string ErrorTypeSafe400 = """
        {"detail":{"error_type":"api_usage_error","message":"Unknown model: nope"}}
        """;

    // 錯誤格式：Cloudflare Workers AI
    public const string ErrorCloudflare = """
        {"result":null,"success":false,
         "errors":[{"code":5007,"message":"No such model @cf/cloudflare/nope"}],"messages":[]}
        """;

    // 錯誤格式：Ollama / llama-server，error 是字串
    public const string ErrorPlainString = """{"error":"model not found"}""";

    // Cloudflare 成功信封（依文件手寫，未經真端點驗證）
    public const string CloudflareOk = """
        {"result":{"model":"clef-flash","answers":{"big":{"type":"noul","noul":0.9}},
                   "usage":{"input_tokens":10,"output_tokens":0}},
         "success":true,"errors":[],"messages":[]}
        """;

    // Cloudflare success:true 但 result 缺
    public const string CloudflareSuccessNoResult = """{"result":null,"success":true,"errors":[],"messages":[]}""";
}