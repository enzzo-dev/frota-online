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

    /// <summary>Upper-case letters and digits only: strips punctuation such as dots, slash and dash.</summary>
    public static string NormalizeTaxId(string? value) =>
        new string((value ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>True for a valid CPF (11 digits) or a valid CNPJ (numeric or alphanumeric).</summary>
    public static bool IsValidTaxId(string? value)
    {
        var normalized = NormalizeTaxId(value);
        return normalized.Length switch
        {
            11 => IsValidCpf(normalized),
            14 => IsValidCnpj(normalized),
            _ => false
        };
    }

    /// <summary>
    /// Validates a CNPJ, including the alphanumeric format: 12 letters or digits followed by 2 check digits.
    /// Each character counts as its ASCII code minus 48, so numeric CNPJs keep working unchanged.
    /// </summary>
    public static bool IsValidCnpj(string? value)
    {
        var cnpj = NormalizeTaxId(value);
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1 || !char.IsAsciiDigit(cnpj[12]) || !char.IsAsciiDigit(cnpj[13]))
        {
            return false;
        }

        var first = CnpjDigit(cnpj.AsSpan(0, 12), FirstCnpjWeights);
        var second = CnpjDigit(cnpj.AsSpan(0, 13), SecondCnpjWeights);
        return cnpj[12] - '0' == first && cnpj[13] - '0' == second;
    }

    private static readonly int[] FirstCnpjWeights = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    private static readonly int[] SecondCnpjWeights = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    private static int CnpjDigit(ReadOnlySpan<char> chars, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < chars.Length; i++)
        {
            sum += (chars[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
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
