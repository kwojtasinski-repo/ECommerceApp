namespace ECommerceApp.Application.AccountProfile.Options
{
    /// <summary>
    /// Configures the unclaimed-guest-profile retention cleanup job (ADR-0030 Phase 4).
    /// Bind from appsettings.json under the "GuestProfileCleanup" section. Defaults apply when the
    /// section is absent. <see cref="RetentionDays"/>'s default (90) is ADR-0030's placeholder value,
    /// not a confirmed business/legal decision — kept configurable specifically so it can change
    /// without a code deploy once that discussion concludes.
    /// </summary>
    public sealed class GuestProfileCleanupOptions
    {
        public const string SectionName = "GuestProfileCleanup";

        /// <summary>When false, the cleanup job runs but deletes nothing.</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>Unclaimed profiles older than this many days become eligible for deletion.</summary>
        public int RetentionDays { get; init; } = 90;

        /// <summary>
        /// Cron expression for the recurring <c>ScheduledJob</c> row, reconciled at startup by
        /// <c>GuestProfileCleanupScheduledJobReconciler</c> (mirrors <c>MessagingOptions</c>'s
        /// Outbox/Inbox cleanup schedules). Default: daily at 04:00 UTC.
        /// </summary>
        public string Schedule { get; init; } = "0 4 * * *";
    }
}
