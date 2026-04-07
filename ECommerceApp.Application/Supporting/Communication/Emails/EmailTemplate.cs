using System.Collections.Generic;

namespace ECommerceApp.Application.Supporting.Communication.Emails
{
    /// <summary>
    /// Channel-agnostic email template owned by the Communication BC.
    /// Callers (handlers inside this BC) supply structured data;
    /// the Infrastructure layer is responsible for rendering and delivery.
    /// </summary>
    public sealed record EmailTemplate(
        string To,
        string Subject,
        string Body,
        IReadOnlyList<EmailAction>? Actions = null);
}
