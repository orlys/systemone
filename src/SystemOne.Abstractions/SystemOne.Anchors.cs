public static partial class SystemOne
{
    /// <summary>提供者的掛載點：<c>SystemOne.Providers.UseXxx(...)</c></summary>
    /// <remarks>提供者套件以 <c>this SystemOneProviders</c> 擴充方法加入自己的 <c>Use*</c></remarks>
    public static SystemOneProviders Providers { get; } = new();

    /// <summary>請求的掛載點：<c>SystemOne.Requests.Noul(...).For(state)</c></summary>
    /// <remarks>其他套件可用 <c>this SystemOneRequests</c> 擴充方法加入預先組好的請求</remarks>
    public static SystemOneRequests Requests { get; } = new();

    /// <summary>權重來源的掛載點：<c>SystemOne.ModelSources.FromDirectory(...)</c></summary>
    /// <remarks>其他套件以 <c>this SystemOneModelSources</c> 擴充方法加入來源，例如 <c>FromHuggingFace</c></remarks>
    public static SystemOneModelSources ModelSources { get; } = new();

    /// <summary>答案、回應與影像的工廠：<c>SystemOne.Factory.Box(...)</c></summary>
    /// <remarks>由此建立，避免直接 <see langword="new"/>；其他套件以 <c>this SystemOneFactory</c> 擴充方法加入自己的工廠</remarks>
    public static SystemOneFactory Factory { get; } = new();
}