# Cospend to SplitDuo Category Mapping

## Updated SplitDuo Category Enum

```csharp
public enum ExpenseCategory
{
    Other = 1,
    Groceries = 2,
    Transportation = 3,
    Utilities = 4,
    Entertainment = 5,
    Health = 6,
    Education = 7,
    Travel = 8,
    Shopping = 9,
    Housing = 10,
    Dining = 11          // NEW: All dining out expenses
}
```

## Category Mapping Table

| Cospend ID | Cospend Category Name  | SplitDuo ID | SplitDuo Category Name |
| ---------- | ---------------------- | ----------- | ---------------------- |
| 5          | Groceries              | 2           | Groceries              |
| 15         | Food and drink - Other | 2           | Groceries              |
| 9          | Liquor                 | 2           | Groceries              |
| 6          | Dining out             | 11          | Dining                 |
| 4          | Car                    | 3           | Transportation         |
| 1          | Bus/train              | 3           | Transportation         |
| 7          | Gas/fuel               | 3           | Transportation         |
| 27         | Transportation - Other | 3           | Transportation         |
| 8          | Plane                  | 3           | Transportation         |
| 30         | Bicycle                | 3           | Transportation         |
| 32         | Parking                | 3           | Transportation         |
| 2          | TV/Phone/Internet      | 4           | Utilities              |
| 21         | Electricity            | 4           | Utilities              |
| 19         | Heat/gas               | 4           | Utilities              |
| 33         | Utilities - Other      | 4           | Utilities              |
| 12         | Trash                  | 4           | Utilities              |
| 25         | Home - Other           | 10          | Housing                |
| 16         | Furniture              | 10          | Housing                |
| 24         | Rent                   | 10          | Housing                |
| 35         | Maintenance            | 10          | Housing                |
| 11         | Entertainment - Other  | 5           | Entertainment          |
| 28         | Music                  | 5           | Entertainment          |
| 31         | Movies                 | 5           | Entertainment          |
| 13         | Sports                 | 5           | Entertainment          |
| 26         | Household supplies     | 9           | Shopping               |
| 14         | Gifts                  | 9           | Shopping               |
| 20         | Clothing               | 9           | Shopping               |
| 17         | Electronics            | 9           | Shopping               |
| 34         | Cleaning               | 9           | Shopping               |
| 36         | Hotel                  | 8           | Travel                 |
| 22         | Medical expenses       | 6           | Health                 |
| 18         | Education              | 7           | Education              |
| 23         | General                | 1           | Other                  |
| 3          | Life - Other           | 1           | Other                  |
| 10         | Taxes                  | 1           | Other                  |
| 29         | Payment                | 1           | Other                  |
| 0          | Unknown                | 1           | Other                  |
| -11        | Unknown                | 1           | Other                  |
