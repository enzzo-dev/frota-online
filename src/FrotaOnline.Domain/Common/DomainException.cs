namespace FrotaOnline.Domain.Common;

/// <summary>Thrown when a business invariant or a state transition rule is violated.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
