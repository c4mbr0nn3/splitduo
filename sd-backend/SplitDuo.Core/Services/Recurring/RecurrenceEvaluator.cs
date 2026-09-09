using System.Globalization;
using Microsoft.Extensions.Localization;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Services.Recurring;

/// <summary>
/// Pure evaluator for recurrence schedules. DateOnly math only — no time zones, no dependencies.
/// Not a static class: <see cref="Describe"/> resolves resources via IStringLocalizer&lt;RecurrenceEvaluator&gt;,
/// and static types cannot be used as generic type arguments. All members are static regardless.
/// </summary>
public class RecurrenceEvaluator
{
    public sealed record RecurrenceSpec(
        RecurrenceMode Mode,
        int Weekdays,
        int? DayOfMonth,
        int? Interval,
        DateOnly AnchorDate,
        DateOnly? EndDate);

    /// <summary>
    /// Returns all occurrences of the spec within the inclusive window [from, to], ordered ascending.
    /// Occurrences before AnchorDate are never returned; EndDate is inclusive.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetOccurrences(RecurrenceSpec spec, DateOnly from, DateOnly to)
    {
        Validate(spec);
        if (from > to)
        {
            return [];
        }

        var start = from > spec.AnchorDate ? from : spec.AnchorDate;
        if (start > to)
        {
            return [];
        }

        var effectiveEnd = spec.EndDate.HasValue && spec.EndDate.Value < to ? spec.EndDate.Value : to;
        if (start > effectiveEnd)
        {
            return [];
        }

        return spec.Mode switch
        {
            RecurrenceMode.Weekly => GetWeekly(spec, start, effectiveEnd),
            RecurrenceMode.Monthly => GetMonthly(spec, start, effectiveEnd),
            RecurrenceMode.EveryNWeeks => GetEveryNWeeks(spec, start, effectiveEnd),
            RecurrenceMode.EveryNMonths => GetEveryNMonths(spec, start, effectiveEnd),
            _ => [],
        };
    }

    /// <summary>
    /// Culture-independent description of the spec: a resx template key plus the raw values
    /// needed to fill it. No rendered text — localized rendering happens in <see cref="Describe"/>.
    /// </summary>
    /// <param name="TemplateKey">Resx key in RecurrenceEvaluator resources (e.g. "WeeklyOn").</param>
    /// <param name="Weekdays">Weekdays referenced by the spec (Weekly: all selected bits; EveryNWeeks: the anchor weekday).</param>
    /// <param name="DayOfMonth">Day-of-month for Monthly mode, or the anchor day for EveryNMonths mode.</param>
    /// <param name="Interval">Interval for EveryN* modes.</param>
    public sealed record DescribeParts(
        string TemplateKey,
        IReadOnlyList<DayOfWeek> Weekdays,
        int? DayOfMonth,
        int? Interval);

    /// <summary>
    /// Culture-independent parts of the spec description (see <see cref="DescribeParts"/>).
    /// Pure and testable without any localization dependency.
    /// </summary>
    public static DescribeParts GetDescribeParts(RecurrenceSpec spec)
    {
        Validate(spec);
        return spec.Mode switch
        {
            RecurrenceMode.Weekly => new DescribeParts("WeeklyOn", WeekdaysFromBits(spec.Weekdays), null, null),
            RecurrenceMode.Monthly => new DescribeParts("MonthlyOnThe", [], spec.DayOfMonth, null),
            RecurrenceMode.EveryNWeeks => new DescribeParts("EveryNWeeksOn", [spec.AnchorDate.DayOfWeek], null, spec.Interval),
            RecurrenceMode.EveryNMonths => new DescribeParts("EveryNMonthsOnThe", [], spec.AnchorDate.Day, spec.Interval),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Mode, "Unknown recurrence mode."),
        };
    }

    /// <summary>
    /// Human-readable, localized summary of the spec, e.g. "Weekly on Monday, Wednesday", "Monthly on the 15th",
    /// "Every 2 weeks on Monday", "Every 3 months on the 28th" (English) or "Settimanale il lunedì, mercoledì",
    /// "Mensile il 15", "Ogni 2 settimane il lunedì", "Ogni 3 mesi il 28" (Italian).
    /// The caller injects the resx-backed localizer so the evaluator stays pure/static.
    /// </summary>
    public static string Describe(RecurrenceSpec spec, IStringLocalizer<RecurrenceEvaluator> loc)
    {
        var parts = GetDescribeParts(spec);
        return parts.TemplateKey switch
        {
            "WeeklyOn" => string.Format(loc["WeeklyOn"].Value, WeekdayNames(parts.Weekdays, loc)),
            "MonthlyOnThe" => string.Format(loc["MonthlyOnThe"].Value, DayLabel(parts.DayOfMonth!.Value, loc)),
            "EveryNWeeksOn" => string.Format(loc["EveryNWeeksOn"].Value, parts.Interval, WeekdayName(parts.Weekdays[0], loc)),
            "EveryNMonthsOnThe" => string.Format(loc["EveryNMonthsOnThe"].Value, parts.Interval, DayLabel(parts.DayOfMonth!.Value, loc)),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), parts.TemplateKey, "Unknown description template key."),
        };
    }

    // --- Weekday/day rendering helpers ---

    private static IReadOnlyList<DayOfWeek> WeekdaysFromBits(int weekdays)
    {
        var all = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };
        return all.Where(d => IsWeekdaySet(weekdays, d)).ToList();
    }

    private static string WeekdayNames(IReadOnlyList<DayOfWeek> days, IStringLocalizer<RecurrenceEvaluator> loc) =>
        string.Join(", ", days.Select(d => WeekdayName(d, loc)));

    private static string WeekdayName(DayOfWeek day, IStringLocalizer<RecurrenceEvaluator> loc) => day switch
    {
        DayOfWeek.Monday => loc["WeekdayMonday"].Value,
        DayOfWeek.Tuesday => loc["WeekdayTuesday"].Value,
        DayOfWeek.Wednesday => loc["WeekdayWednesday"].Value,
        DayOfWeek.Thursday => loc["WeekdayThursday"].Value,
        DayOfWeek.Friday => loc["WeekdayFriday"].Value,
        DayOfWeek.Saturday => loc["WeekdaySaturday"].Value,
        DayOfWeek.Sunday => loc["WeekdaySunday"].Value,
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown day of week."),
    };

    /// <summary>
    /// Renders the day-of-month for "Monthly on the X" / "Every N months on the X".
    /// Ordinal decision: the suffix is language-specific (English needs 1st/15th/31st, Italian
    /// uses the plain number), so it cannot live in the resx format template. Each resx carries
    /// a "UseOrdinalSuffixes" flag ("true"/"false") that drives the decision, keeping rendering
    /// consistent with the localizer's actual language regardless of ambient thread culture.
    /// </summary>
    private static string DayLabel(int day, IStringLocalizer<RecurrenceEvaluator> loc)
    {
        var useOrdinals = string.Equals(loc["UseOrdinalSuffixes"].Value, "true", StringComparison.OrdinalIgnoreCase);
        return useOrdinals ? Ordinal(day) : day.ToString(CultureInfo.CurrentCulture);
    }

    private static string Ordinal(int day) => day switch
    {
        1 or 21 or 31 => $"{day}st",
        2 or 22 => $"{day}nd",
        3 or 23 => $"{day}rd",
        _ => $"{day}th",
    };

    // --- Weekly ---

    private static List<DateOnly> GetWeekly(RecurrenceSpec spec, DateOnly start, DateOnly end)
    {
        var result = new List<DateOnly>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (IsWeekdaySet(spec.Weekdays, day.DayOfWeek))
            {
                result.Add(day);
            }
        }

        return result;
    }

    private static bool IsWeekdaySet(int weekdays, DayOfWeek day) =>
        (weekdays & (1 << ((int)day == 0 ? 6 : (int)day - 1))) != 0;

    private static string DescribeWeekdays(int weekdays)
    {
        var names = new[]
        {
            (DayOfWeek.Monday, "Monday"),
            (DayOfWeek.Tuesday, "Tuesday"),
            (DayOfWeek.Wednesday, "Wednesday"),
            (DayOfWeek.Thursday, "Thursday"),
            (DayOfWeek.Friday, "Friday"),
            (DayOfWeek.Saturday, "Saturday"),
            (DayOfWeek.Sunday, "Sunday"),
        };
        return string.Join(", ", names.Where(n => IsWeekdaySet(weekdays, n.Item1)).Select(n => n.Item2));
    }

    // --- Monthly ---

    private static List<DateOnly> GetMonthly(RecurrenceSpec spec, DateOnly start, DateOnly end)
    {
        var result = new List<DateOnly>();
        var day = spec.DayOfMonth!.Value;
        var month = new DateOnly(start.Year, start.Month, 1);
        var endMonth = new DateOnly(end.Year, end.Month, 1);
        while (month <= endMonth)
        {
            var occurrence = ClampToMonth(month, day);
            if (occurrence >= start && occurrence <= end)
            {
                result.Add(occurrence);
            }

            month = month.AddMonths(1);
        }

        return result;
    }

    private static DateOnly ClampToMonth(DateOnly monthStart, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        return monthStart.AddDays(Math.Min(day, daysInMonth) - 1);
    }

    // --- EveryNWeeks ---

    private static List<DateOnly> GetEveryNWeeks(RecurrenceSpec spec, DateOnly start, DateOnly end)
    {
        var interval = spec.Interval!.Value;
        var result = new List<DateOnly>();
        var anchor = spec.AnchorDate;

        // First anchor-multiple on/after start: offset in weeks, rounded up (ceiling)
        // to the next 7-day grid line, then rounded up again to the next interval multiple.
        // Ceiling is required: truncation can land below `start` and skip a valid
        // occurrence when the window starts mid-interval (e.g. anchor Jan 1, interval 2,
        // window starting Jan 9 must yield Jan 15, not jump straight to Jan 29).
        var weeksFromAnchor = (int)Math.Ceiling((start.DayNumber - (double)anchor.DayNumber) / 7.0);
        var remainder = weeksFromAnchor % interval;
        if (remainder != 0)
        {
            weeksFromAnchor += interval - remainder;
        }

        for (var i = weeksFromAnchor; ; i += interval)
        {
            var occurrence = anchor.AddDays(i * 7);
            if (occurrence > end)
            {
                break;
            }

            result.Add(occurrence);
        }

        return result;
    }

    // --- EveryNMonths ---

    /// <remarks>
    /// Documented deviation: stepping is per-period from the LAST computed occurrence, not recomputed
    /// from the anchor. Monthly from Jan 31 yields Jan 31 → Feb 28 → Mar 28 → Apr 28 — the day-of-month
    /// never recovers to the 31st. Note: <see cref="DateOnly.AddMonths(int)"/> already clamps overflow
    /// (Feb 31 → Feb 28/29), so stepping is a plain AddMonths on the previous occurrence.
    /// </remarks>
    private static List<DateOnly> GetEveryNMonths(RecurrenceSpec spec, DateOnly start, DateOnly end)
    {
        var interval = spec.Interval!.Value;
        var result = new List<DateOnly>();

        // Fast-forward from the anchor to the first occurrence on/after `start`,
        // applying the same per-period clamped stepping as the forward iteration.
        var current = spec.AnchorDate;
        while (current < start)
        {
            var next = current.AddMonths(interval);
            if (next <= current)
            {
                break; // Degenerate guard: never loop forever.
            }

            current = next;
        }

        while (current <= end)
        {
            result.Add(current);
            current = current.AddMonths(interval);
        }

        return result;
    }

    // --- Validation ---

    private static void Validate(RecurrenceSpec spec)
    {
        switch (spec.Mode)
        {
            case RecurrenceMode.Weekly:
                if (spec.Weekdays == 0)
                {
                    throw new ArgumentException("Weekly mode requires at least one weekday bit set.", nameof(spec));
                }

                if ((spec.Weekdays & ~0x7F) != 0)
                {
                    throw new ArgumentException("Weekdays contains bits outside the Monday..Sunday range.", nameof(spec));
                }

                break;
            case RecurrenceMode.Monthly:
                if (spec.DayOfMonth is null || spec.DayOfMonth < 1 || spec.DayOfMonth > 31)
                {
                    throw new ArgumentException("Monthly mode requires DayOfMonth between 1 and 31.", nameof(spec));
                }

                break;
            case RecurrenceMode.EveryNWeeks:
            case RecurrenceMode.EveryNMonths:
                if (spec.Interval is null || spec.Interval < 1)
                {
                    throw new ArgumentException("EveryN* modes require Interval >= 1.", nameof(spec));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(spec), spec.Mode, "Unknown recurrence mode.");
        }
    }
}