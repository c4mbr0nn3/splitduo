using CsvHelper;

namespace SplitDuo.Core.Dto.Imports;

[CsvRecord]
public partial class SplitDuoAliasDto
{
    [CsvColumn("name", Optional = true)] public string Name { get; set; } = "";
    [CsvColumn("is_singleton", Optional = true)] public int IsSingletonInt { get; set; }

    [CsvIgnore] public bool IsSingleton => IsSingletonInt == 1;
}

[CsvRecord]
public partial class SplitDuoAliasMemberDto
{
    [CsvColumn("email", Optional = true)] public string Email { get; set; } = "";
    [CsvColumn("alias_name", Optional = true)] public string AliasName { get; set; } = "";
    [CsvColumn("role", Optional = true)] public string Role { get; set; } = "";
}

[CsvRecord]
public partial class SplitDuoAliasExpenseDto
{
    [CsvColumn("date", Optional = true)] public string Date { get; set; } = "";
    [CsvColumn("title", Optional = true)] public string Title { get; set; } = "";
    [CsvColumn("description", Optional = true)] public string? Description { get; set; }
    [CsvColumn("amount", Optional = true)] public decimal Amount { get; set; }
    [CsvColumn("paid_by_email", Optional = true)] public string PaidByEmail { get; set; } = "";
    [CsvColumn("paid_by_alias_name", Optional = true)] public string PaidByAliasName { get; set; } = "";
    [CsvColumn("category", Optional = true)] public string Category { get; set; } = "";
    [CsvColumn("payment_mode", Optional = true)] public string PaymentMode { get; set; } = "";
    [CsvColumn("alias_splits", Optional = true)] public string AliasSplits { get; set; } = "";

    [CsvIgnore] public DateOnly ParsedDate => DateOnly.Parse(Date);

    [CsvIgnore]
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
