public static partial class SystemOne
{
    /// <summary>地端模型權重的來源；<see cref="ResolveAsync"/> 回傳含權重的本機目錄</summary>
    /// <remarks>由 <see cref="ModelSources"/> 建立；提供者套件（Laya、Kev）只依賴這個型別</remarks>
    public abstract class ModelSource
    {
        /// <summary>取得本機目錄；需要時先下載</summary>
        /// <param name="progress">下載進度回報</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>含權重的目錄路徑</returns>
        /// <exception cref="DirectoryNotFoundException">目錄不存在</exception>
        /// <exception cref="SystemOneException">下載或驗證失敗</exception>
        /// <exception cref="IOException">寫入快取目錄失敗</exception>
        /// <exception cref="OperationCanceledException">已取消</exception>
        public abstract Task<string> ResolveAsync(
            IProgress<ModelDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        internal sealed class DirectoryModelSource : ModelSource
        {
            private readonly string _path;

            public DirectoryModelSource(string path)
            {
                _path = path;
            }

            public override Task<string> ResolveAsync(
                IProgress<ModelDownloadProgress>? progress = null,
                CancellationToken cancellationToken = default)
            {
                if (!Directory.Exists(_path))
                {
                    // 指定的權重目錄不存在，沒有東西可以載入
                    throw new DirectoryNotFoundException($"Model directory not found: {_path}");
                }

                return Task.FromResult(_path);
            }
        }
    }

    /// <summary>下載進度：目前檔案與位元組</summary>
    public sealed record ModelDownloadProgress
    {
        /// <summary>目前下載的檔案（相對於存放庫）</summary>
        public required string File { get; set; } = default!;

        /// <summary>已收到的位元組</summary>
        public long BytesReceived { get; set; }

        /// <summary>總位元組</summary>
        /// <remarks>未知時為 0</remarks>
        public long TotalBytes { get; set; }
    }
}