using System;

namespace ECommerceApp.Application.Catalog.Images.ChunkedUpload
{
    /// <summary>
    /// Settings of the custom ("Classic") chunked image upload. Bound from the
    /// <c>Catalog:ChunkedUpload</c> configuration section; defaults apply when it is absent.
    /// File size and extension limits are intentionally NOT here — they come from <c>ImageConstraints</c>.
    /// </summary>
    public sealed class ChunkedUploadOptions
    {
        public const string SectionName = "Catalog:ChunkedUpload";

        /// <summary>When false, the cleanup job runs but removes nothing.</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>Maximum number of simultaneously open upload sessions of a single user.</summary>
        public int MaxOpenSessionsPerUser { get; init; } = 5;

        /// <summary>Sessions with no activity for this long are removed (memory and disk) by the cleanup job.</summary>
        public TimeSpan SessionTtl { get; init; } = TimeSpan.FromHours(24);

        /// <summary>Cron expression of the recurring cleanup job. Default: hourly.</summary>
        public string Schedule { get; init; } = "0 * * * *";

        /// <summary>
        /// Root directory of the temporary upload sessions. Defaults to <c>{CurrentDirectory}/Upload/Temp</c>
        /// when empty; overridable mainly for tests.
        /// </summary>
        public string TempPath { get; init; }
    }
}
