public static partial class SystemOne
{
    /// <summary>隨請求送出的影像</summary>
    /// <remarks>
    /// 只送位元組，不送遠端 URL；以請求鏈的 <c>.Image(...)</c> 或 <c>SystemOne.Factory.Image(...)</c> 建立
    /// </remarks>
    public sealed record Image
    {
        /// <summary>原始影像位元組</summary>
        public required ReadOnlyMemory<byte> Bytes { get; set; } = default!;

        /// <summary>png、jpg、jpeg 或 webp</summary>
        /// <remarks>TypeSafe 只收前三種</remarks>
        public required string Format { get; set; } = default!;
    }
}