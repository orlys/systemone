/// <summary><see cref="SystemOne.ModelSources"/> 的型別：權重來源的進入點，其他來源以擴充方法掛在這裡</summary>
/// <remarks>
/// 來源套件寫 <c>public static SystemOne.ModelSource FromXxx(this SystemOneModelSources anchor, ...)</c>，
/// 呼叫端就能寫 <c>SystemOne.ModelSources.FromXxx(...)</c>
/// </remarks>
public sealed class SystemOneModelSources
{
    internal SystemOneModelSources()
    {
    }

    /// <summary>已放在本機的權重目錄（離線環境）</summary>
    /// <param name="path">目錄路徑</param>
    /// <returns>指向該目錄的來源</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> 為空白</exception>
    public SystemOne.ModelSource FromDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new SystemOne.ModelSource.DirectoryModelSource(path);
    }
}