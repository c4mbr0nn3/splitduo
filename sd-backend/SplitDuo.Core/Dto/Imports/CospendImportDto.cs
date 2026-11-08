using System.Text.Json.Serialization;
using CsvHelper;

namespace SplitDuo.Core.Dto.Imports;

[CsvRecord]
public partial class CospendExpenseDto
{
    [CsvColumn("what", Optional = true)] public string What { get; set; } = "";
    [CsvColumn("amount", Optional = true)] public decimal Amount { get; set; }
    [CsvColumn("date", Optional = true)] public string Date { get; set; } = "";
    [CsvColumn("timestamp", Optional = true)] public long Timestamp { get; set; }
    [CsvColumn("payer_name", Optional = true)] public string PayerName { get; set; } = "";
    [CsvColumn("owers", Optional = true)] public string Owers { get; set; } = "";
    [CsvColumn("categoryid", Optional = true)] public int CategoryId { get; set; }
    [CsvColumn("paymentmodeid", Optional = true)] public int PaymentModeId { get; set; }
    [CsvColumn("comment", Optional = true)] public string Comment { get; set; } = "";
    [CsvColumn("deleted", Optional = true)] public int Deleted { get; set; }

    [CsvIgnore] public bool IsDeleted => Deleted == 1;

    [CsvIgnore] public DateOnly ParsedDate => DateOnly.Parse(Date);
    [CsvIgnore] public DateTime ParsedTimestamp => DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;

    [CsvIgnore]
    public List<string> OwersNames =>
        Owers.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToList();
}

[CsvRecord]
public partial class CospendMemberDto
{
    [CsvColumn("name", Optional = true)] public string Name { get; set; } = "";
    [CsvColumn("weight", Optional = true)] public int Weight { get; set; }
    [CsvColumn("active", Optional = true)] public int Active { get; set; }
    [CsvColumn("color", Optional = true)] public string Color { get; set; } = "";

    [CsvIgnore] public bool IsActive => Active == 1;

    public KeyValueDto ToKeyValueDto() => new() { Key = Name, Value = Name };
}

[CsvRecord]
public partial class CospendCategoryDto
{
    [CsvColumn("categoryname", Optional = true)] public string CategoryName { get; set; } = "";
    [CsvColumn("categoryid", Optional = true)] public int CategoryId { get; set; }
    [CsvColumn("icon", Optional = true)] public string Icon { get; set; } = "";
    [CsvColumn("color", Optional = true)] public string Color { get; set; } = "";

    public KeyValueDto ToKeyValueDto() => new() { Key = CategoryId.ToString(), Value = CategoryName };
}

[CsvRecord]
public partial class CospendPaymentModeDto
{
    [CsvColumn("paymentmodename", Optional = true)] public string PaymentModeName { get; set; } = "";
    [CsvColumn("paymentmodeid", Optional = true)] public int PaymentModeId { get; set; }
    [CsvColumn("icon", Optional = true)] public string Icon { get; set; } = "";
    [CsvColumn("color", Optional = true)] public string Color { get; set; } = "";

    public KeyValueDto ToKeyValueDto() => new() { Key = PaymentModeId.ToString(), Value = PaymentModeName };
}

public class KeyValueDto
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
}

public enum CospendSection
{
    None,
    Members,
    Expenses,
    Categories,
    PaymentModes
}