using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Validation;

/// <summary>
/// Validates password complexity requirements without external libraries.
/// Enforces minimum length, uppercase, lowercase, digit, and special character rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PasswordComplexityAttribute : ValidationAttribute
{
    private const int MinimumLength = 8;
    private const string SpecialCharacters = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    public override bool IsValid(object? value)
    {
        if (value is not string password || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Password is required.";
            return false;
        }

        var errors = new List<string>();

        // Check minimum length
        if (password.Length < MinimumLength)
        {
            errors.Add($"at least {MinimumLength} characters");
        }

        // Check for uppercase letter
        if (!password.Any(char.IsUpper))
        {
            errors.Add("at least one uppercase letter");
        }

        // Check for lowercase letter
        if (!password.Any(char.IsLower))
        {
            errors.Add("at least one lowercase letter");
        }

        // Check for digit
        if (!password.Any(char.IsDigit))
        {
            errors.Add("at least one digit");
        }

        // Check for special character
        if (!password.Any(c => SpecialCharacters.Contains(c)))
        {
            // Brace characters in the message are escaped (doubled) because
            // ValidationAttribute.FormatErrorMessage runs string.Format on ErrorMessage,
            // which would otherwise throw FormatException on literal '{' / '}'.
            errors.Add($"at least one special character ({SpecialCharacters.Replace("{", "{{").Replace("}", "}}")})");
        }

        if (errors.Count > 0)
        {
            ErrorMessage = $"Password must contain {string.Join(", ", errors)}.";
            return false;
        }

        return true;
    }
}