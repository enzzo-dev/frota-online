namespace FrotaOnline.Application.Common;

public static class BrazilianDocuments
{
    public static string OnlyDigits(string? value) =>
        new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    /// <summary>Validates the CPF check digits. Punctuation is ignored.</summary>
    public static bool IsValidCpf(string? value)
    {
        var digits = OnlyDigits(value);
        if (digits.Length != 11 || digits.Distinct().Count() == 1)
        {
            return false;
        }

        return CheckDigit(digits, 9, 10) == digits[9] - '0'
            && CheckDigit(digits, 10, 11) == digits[10] - '0';
    }

    /// <summary>Accepts the old (ABC-1234) and Mercosul (ABC1D23) plate formats.</summary>
    public static bool IsValidPlate(string? value) =>
        System.Text.RegularExpressions.Regex.IsMatch(NormalizePlate(value), "^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$");

    public static string NormalizePlate(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static int CheckDigit(string digits, int length, int firstWeight)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (digits[i] - '0') * (firstWeight - i);
        }

        var remainder = sum * 10 % 11;
        return remainder == 10 ? 0 : remainder;
    }
}
