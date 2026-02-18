namespace SplitDuo.Core.Dto.Imports;

public class SplitwiseExpenseRow
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Cost { get; set; }
    public string PayerName { get; set; } = "";
    public List<SplitwiseOwer> Owers { get; set; } = [];
}

public class SplitwiseOwer
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; } // absolute (already positive)
}

public class SplitwiseParseResult
{
    public List<SplitwiseExpenseRow> Expenses { get; set; } = [];
    public HashSet<string> Members { get; set; } = [];
    public List<KeyValueDto> Categories { get; set; } = []; // Key=sequential int string, Value=name
}