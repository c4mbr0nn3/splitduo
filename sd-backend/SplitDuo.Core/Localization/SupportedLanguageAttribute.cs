using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Core.Localization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SupportedLanguageAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string language && !SupportedLanguages.IsSupported(language))
        {
            return new ValidationResult(ErrorMessage ?? "Unsupported language");
        }

        return ValidationResult.Success;
    }
}
