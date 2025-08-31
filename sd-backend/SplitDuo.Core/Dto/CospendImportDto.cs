using CsvHelper.Configuration.Attributes;

namespace SplitDuo.Core.Dto;

public class CospendExpenseDto
{
    [Name("what")] public string What { get; set; } = "";
    [Name("amount")] public decimal Amount { get; set; }
    [Name("date")] public string Date { get; set; } = "";
    [Name("payer_name")] public string PayerName { get; set; } = "";
    [Name("owers")] public string Owers { get; set; } = "";
    [Name("categoryid")] public int CategoryId { get; set; }
    [Name("paymentmodeid")] public int PaymentModeId { get; set; }
    [Name("comment")] public string Comment { get; set; } = "";
    [Name("deleted")] public int Deleted { get; set; }

    [Ignore] public bool IsDeleted => Deleted == 1;

    [Ignore] public DateOnly ParsedDate => DateOnly.Parse(Date);

    [Ignore]
    public List<string> OwersNames =>
        Owers.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToList();
}