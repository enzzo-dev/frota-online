namespace FrotaOnline.Domain.Common;

internal static class Guard
{
    public static string NotNullOrWhiteSpace(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} é obrigatório.");
        }

        return value.Trim();
    }

    public static decimal Positive(decimal value, string name)
    {
        if (value <= 0)
        {
            throw new DomainException($"{name} deve ser maior que zero.");
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{name} é obrigatório.");
        }

        return value;
    }
}
