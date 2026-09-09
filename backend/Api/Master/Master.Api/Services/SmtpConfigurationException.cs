namespace Master.Api.Services;

/// <summary>
/// The branch's mail settings are wrong or missing, and the administrator
/// reading the answer is the person who can fix it.
///
/// A type of its own rather than <see cref="InvalidOperationException"/>,
/// which is what both throw sites used before. That mattered: the controller
/// caught <c>InvalidOperationException</c> and returned its message as a 400,
/// and .NET raises that same type for a great many things nobody outside the
/// building should read — <c>TenantContext.Require()</c> raises it naming the
/// claims a request was missing, and EF raises it for connection and change
/// tracker state. A caller was one unrelated fault away from being handed
/// either, labelled as their own bad request.
///
/// Catching this type instead means only the message written here can reach a
/// caller, and everything else takes the ordinary route through
/// <c>GlobalExceptionHandler</c> and the error log.
/// </summary>
public sealed class SmtpConfigurationException : Exception
{
    public SmtpConfigurationException(string message) : base(message)
    {
    }
}
