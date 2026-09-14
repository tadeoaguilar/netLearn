namespace CleanArchitecture.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would leave an entity in an invalid state.
///
/// The domain defines its own exception type rather than throwing
/// InvalidOperationException, so outer layers can translate "you broke a
/// business rule" (a 400) apart from "something went wrong" (a 500).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
