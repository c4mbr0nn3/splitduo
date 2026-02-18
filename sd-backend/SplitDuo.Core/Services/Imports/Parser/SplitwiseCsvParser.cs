using System.Globalization;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports.Parser;

public static class SplitwiseCsvParser
{
    public static async Task<SplitwiseParseResult> ParseAsync(byte[] file)
    {
        await using var stream = new MemoryStream(file);
        return await ParseAsync(stream);
    }

    public static async Task<SplitwiseParseResult> ParseAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await ParseAsync(stream);
    }

    private static async Task<SplitwiseParseResult> ParseAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.None);

        // Find header line (first non-empty line)
        string? headerLine = null;
        var dataStartIndex = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            headerLine = trimmed;
            dataStartIndex = i + 1;
            break;
        }

        if (headerLine == null)
            return new SplitwiseParseResult();

        // Parse header: Date,Description,Category,Cost,Currency,Participant1,Participant2,...
        var headers = SplitCsvLine(headerLine);
        if (headers.Count < 6)
            return new SplitwiseParseResult();

        var participants = headers.Skip(5).ToList(); // columns 5+ are participant names

        var expenses = new List<SplitwiseExpenseRow>();
        var members = new HashSet<string>(participants);
        var categoryOrder = new List<string>();
        var categorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = dataStartIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var fields = SplitCsvLine(line);
            if (fields.Count < 5)
                continue;

            // Skip if Cost is blank or unparseable
            var costStr = fields[3].Trim();
            if (string.IsNullOrWhiteSpace(costStr) ||
                !decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var cost))
                continue;

            // Skip zero-cost rows (catches "Total balance" and placeholder rows)
            if (cost == 0)
                continue;

            var category = fields[2].Trim().Trim('"');

            // Skip "Payment" category rows (debt settlements)
            if (string.Equals(category, "Payment", StringComparison.OrdinalIgnoreCase))
                continue;

            // Parse participant values
            string? payerName = null;
            var owers = new List<SplitwiseOwer>();

            for (var p = 0; p < participants.Count; p++)
            {
                var colIndex = 5 + p;
                if (colIndex >= fields.Count)
                    break;

                var valueStr = fields[colIndex].Trim();
                if (string.IsNullOrWhiteSpace(valueStr) ||
                    !decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    continue;

                if (value > 0)
                {
                    // Multiple payers: skip row
                    if (payerName != null)
                    {
                        payerName = null;
                        break;
                    }

                    payerName = participants[p];
                }
                else if (value < 0)
                {
                    owers.Add(new SplitwiseOwer
                    {
                        Name = participants[p],
                        Amount = Math.Abs(value)
                    });
                }
                // value == 0: not involved, skip
            }

            // Skip if no payer identified
            if (payerName == null)
                continue;

            if (!categorySet.Contains(category))
            {
                categorySet.Add(category);
                categoryOrder.Add(category);
            }

            var dateStr = fields[0].Trim().Trim('"');
            if (!DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
                continue;

            expenses.Add(new SplitwiseExpenseRow
            {
                Date = date,
                Description = fields[1].Trim().Trim('"'),
                Category = category,
                Cost = cost,
                PayerName = payerName,
                Owers = owers
            });
        }

        var categories = categoryOrder
            .Select((name, idx) => new KeyValueDto { Key = idx.ToString(), Value = name })
            .ToList();

        return new SplitwiseParseResult
        {
            Expenses = expenses,
            Members = members,
            Categories = categories
        };
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}