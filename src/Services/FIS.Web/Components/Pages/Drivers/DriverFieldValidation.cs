using System.Globalization;

namespace FIS.Web.Components.Pages.Drivers;

internal static class DriverFieldValidation
{
    public static string? NormalizeSouthAfricanId(string? value)
        => NormalizeCompact(value, uppercase: false);

    public static string? NormalizePassportNumber(string? value)
        => NormalizeCompact(value, uppercase: true);

    public static string? NormalizeLicenceNumber(string? value)
        => NormalizeCompact(value, uppercase: true);

    public static string? ValidateSouthAfricanId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length != 13 || !value.All(char.IsDigit))
        {
            return "South African ID number must be 13 digits.";
        }

        if (!TryParseBirthDate(value))
        {
            return "South African ID number must contain a valid birth date.";
        }

        if (!PassesLuhnChecksum(value))
        {
            return "South African ID number is not valid.";
        }

        return null;
    }

    public static string? ValidatePassportNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.All(char.IsLetterOrDigit)
            ? null
            : "Passport number can contain letters and numbers only.";
    }

    public static string? ValidateLicenceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "The licence number is required.";
        }

        if (value.Length != 12)
        {
            return "Licence number must be 8 digits followed by 4 letters.";
        }

        return value[..8].All(char.IsDigit) && value[8..].All(char.IsLetter)
            ? null
            : "Licence number must be 8 digits followed by 4 letters.";
    }

    private static string? NormalizeCompact(string? value, bool uppercase)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || character == '-')
            {
                continue;
            }

            buffer[length++] = uppercase ? char.ToUpperInvariant(character) : character;
        }

        return length == 0 ? null : new string(buffer[..length]);
    }

    private static bool TryParseBirthDate(string value)
    {
        var year = int.Parse(value.Substring(0, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(4, 2), CultureInfo.InvariantCulture);
        var currentYear = DateTime.Today.Year % 100;
        var fullYear = year <= currentYear ? 2000 + year : 1900 + year;

        return DateOnly.TryParseExact(
            $"{fullYear:D4}{month:D2}{day:D2}",
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static bool PassesLuhnChecksum(string value)
    {
        var sum = 0;
        var doubleDigit = false;

        for (var index = value.Length - 1; index >= 0; index--)
        {
            var digit = value[index] - '0';

            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
