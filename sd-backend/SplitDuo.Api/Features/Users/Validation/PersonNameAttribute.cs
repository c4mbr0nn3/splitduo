using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PersonNameAttribute : ValidationAttribute
{
    public PersonNameAttribute()
        => ErrorMessage = "Name contains characters that are not allowed.";

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string name => PersonNameRules.IsValid(name),
        _ => false
    };
}
