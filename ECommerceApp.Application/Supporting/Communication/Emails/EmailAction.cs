namespace ECommerceApp.Application.Supporting.Communication.Emails
{
    /// <summary>
    /// A call-to-action button rendered in the email body.
    /// The Infrastructure renderer converts this to an HTML anchor/button.
    /// </summary>
    public sealed record EmailAction(string Label, string Url);
}
