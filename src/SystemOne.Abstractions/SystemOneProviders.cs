/// <summary><see cref="SystemOne.Providers"/> 的型別：提供者擴充方法掛在這裡</summary>
/// <remarks>
/// 提供者套件寫 <c>public static ISystemOne UseXxx(this SystemOneProviders anchor, ...)</c>，
/// 呼叫端就能寫 <c>SystemOne.Providers.UseXxx(...)</c>
/// </remarks>
public sealed class SystemOneProviders
{
    internal SystemOneProviders()
    {
    }
}