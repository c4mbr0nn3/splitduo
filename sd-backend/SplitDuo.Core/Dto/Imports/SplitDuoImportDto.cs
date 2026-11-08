using CsvHelper;

namespace SplitDuo.Core.Dto.Imports;

[CsvRecord]
public partial class SplitDuoExpenseDto
{
    [CsvColumn("Date", Optional = true)] public string Date { get; set; } = "";
    [CsvColumn("Title", Optional = true)] public string Title { get; set; } = "";
    [CsvColumn("Description", Optional = true)] public string? Description { get; set; }
    [CsvColumn("Amount", Optional = true)] public decimal Amount { get; set; }
    [CsvColumn("PaidByEmail", Optional = true)] public string PaidByEmail { get; set; } = "";
    [CsvColumn("Category", Optional = true)] public string Category { get; set; } = "";
    [CsvColumn("PaymentMode", Optional = true)] public string PaymentMode { get; set; } = "";
    [CsvColumn("Owers", Optional = true)] public string Owers { get; set; } = "";

    [CsvIgnore] public DateOnly ParsedDate => DateOnly.Parse(Date);

    [CsvIgnore]
    public List<OwnerSplit> ParsedOwers
    {
        get
        {
            return Owers.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var parts = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException($"Invalid ower format: {part}");

                    return new OwnerSplit
                    {
                        Email = parts[0].Trim(),
                        Amount = decimal.Parse(parts[1].Trim())
                    };
                })
                .ToList();
        }
    }
}

public class OwnerSplit
{
    public string Email { get; set; } = "";
    public decimal Amount { get; set; }
}

public class SplitDuoParseResult
{
    public List<SplitDuoExpenseDto> Expenses { get; set; } = [];
    public HashSet<string> Members { get; set; } = [];
}