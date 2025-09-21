using CsvHelper.Configuration.Attributes;

namespace SplitDuo.Core.Dto.Imports;

public class CospendExpenseDto
{
    [Name("what")] public string What { get; set; } = "";
    [Name("amount")] public decimal Amount { get; set; }
    [Name("date")] public string Date { get; set; } = "";
    [Name("timestamp")] public long Timestamp { get; set; }
    [Name("payer_name")] public string PayerName { get; set; } = "";
    [Name("owers")] public string Owers { get; set; } = "";
    [Name("categoryid")] public int CategoryId { get; set; }
    [Name("paymentmodeid")] public int PaymentModeId { get; set; }
    [Name("comment")] public string Comment { get; set; } = "";
    [Name("deleted")] public int Deleted { get; set; }

    [Ignore] public bool IsDeleted => Deleted == 1;

    [Ignore] public DateOnly ParsedDate => DateOnly.Parse(Date);
    [Ignore] public DateTime ParsedTimestamp => DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;

    [Ignore]
    public List<string> OwersNames =>
        Owers.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToList();
}

public class CospendMemberDto
{
    [Name("name")] public string Name { get; set; } = "";
    [Name("weight")] public int Weight { get; set; }
    [Name("active")] public int Active { get; set; }
    [Name("color")] public string Color { get; set; } = "";

    [Ignore] public bool IsActive => Active == 1;

    public KeyValueDto ToKeyValueDto() => new() { Key = Name, Value = Name };
}

public class CospendCategoryDto
{
    [Name("categoryname")] public string CategoryName { get; set; } = "";
    [Name("categoryid")] public int CategoryId { get; set; }
    [Name("icon")] public string Icon { get; set; } = "";
    [Name("color")] public string Color { get; set; } = "";

    public KeyValueDto ToKeyValueDto() => new() { Key = CategoryId.ToString(), Value = CategoryName };
}

public class CospendPaymentModeDto
{
    [Name("paymentmodename")] public string PaymentModeName { get; set; } = "";
    [Name("paymentmodeid")] public int PaymentModeId { get; set; }
    [Name("icon")] public string Icon { get; set; } = "";
    [Name("color")] public string Color { get; set; } = "";

    public KeyValueDto ToKeyValueDto() => new() { Key = PaymentModeId.ToString(), Value = PaymentModeName };
}

public class KeyValueDto
{
    public string Key { get; set; }
    public string Value { get; set; } = "";
}

public enum CospendSection
{
    None,
    Members,
    Expenses,
    Categories,
    PaymentModes
}