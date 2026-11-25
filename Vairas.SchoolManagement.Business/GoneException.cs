namespace Vairas.SchoolManagement.Business;

/// <summary>The resource existed but is no longer available (for example an expired invitation).</summary>
public sealed class GoneException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
