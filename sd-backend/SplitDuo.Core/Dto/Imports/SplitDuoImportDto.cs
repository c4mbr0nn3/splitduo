using CsvHelper.Configuration.Attributes;

namespace SplitDuo.Core.Dto.Imports;

public class SplitDuoExpenseDto
{
    [Name("Date")] public string Date { get; set; } = "";
    [Name("Title")] public string Title { get; set; } = "";
    [Name("Description")] public string? Description { get; set; }
    [Name("Amount")] public decimal Amount { get; set; }
    [Name("PaidByEmail")] public string PaidByEmail { get; set; } = "";
    [Name("Category")] public string Category { get; set; } = "";
    [Name("PaymentMode")] public string PaymentMode { get; set; } = "";
    [Name("Owers")] public string Owers { get; set; } = "";

    [Ignore] public DateOnly ParsedDate => DateOnly.Parse(Date);

    [Ignore]
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
    public HashSet<string> UniqueEmails { get; set; } = [];
}
