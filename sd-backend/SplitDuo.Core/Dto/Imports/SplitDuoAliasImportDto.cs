using CsvHelper.Configuration.Attributes;

namespace SplitDuo.Core.Dto.Imports;

public class SplitDuoAliasDto
{
    [Name("name")] public string Name { get; set; } = "";
    [Name("is_singleton")] public int IsSingletonInt { get; set; }

    [Ignore] public bool IsSingleton => IsSingletonInt == 1;
}

public class SplitDuoAliasMemberDto
{
    [Name("email")] public string Email { get; set; } = "";
    [Name("alias_name")] public string AliasName { get; set; } = "";
    [Name("role")] public string Role { get; set; } = "";
}

public class SplitDuoAliasExpenseDto
{
    [Name("date")] public string Date { get; set; } = "";
    [Name("title")] public string Title { get; set; } = "";
    [Name("description")] public string? Description { get; set; }
    [Name("amount")] public decimal Amount { get; set; }
    [Name("paid_by_email")] public string PaidByEmail { get; set; } = "";
    [Name("paid_by_alias_name")] public string PaidByAliasName { get; set; } = "";
    [Name("category")] public string Category { get; set; } = "";
    [Name("payment_mode")] public string PaymentMode { get; set; } = "";
    [Name("alias_splits")] public string AliasSplits { get; set; } = "";

    [Ignore] public DateOnly ParsedDate => DateOnly.Parse(Date);

    [Ignore]
    public List<AliasSplitEntry> ParsedAliasSplits
    {
        get
        {
            return AliasSplits.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var parts = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException($"Invalid alias split format: {part}");

                    return new AliasSplitEntry
                    {
                        AliasName = parts[0].Trim(),
                        Amount = decimal.Parse(parts[1].Trim())
                    };
                })
                .ToList();
        }
    }
}

public class AliasSplitEntry
{
    public string AliasName { get; set; } = "";
    public decimal Amount { get; set; }
}

public class SplitDuoAliasParseResult
{
    public List<SplitDuoAliasDto> Aliases { get; set; } = [];
    public List<SplitDuoAliasMemberDto> Members { get; set; } = [];
    public List<SplitDuoAliasExpenseDto> Expenses { get; set; } = [];
}

public enum SplitDuoAliasSection
{
    None,
    Aliases,
    Members,
    Expenses
}
