using System.Globalization;
using Microsoft.Extensions.Localization;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Services.Recurring;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class RecurrenceEvaluatorTests
{
    /// <summary>
    /// Minimal fake localizer for rendering tests: maps keys to fixed format strings,
    /// proving the Describe arg ordering without pulling in resx infrastructure.
    /// </summary>
    private sealed class FakeLocalizer : IStringLocalizer<RecurrenceEvaluator>
    {
        private static readonly Dictionary<string, string> Templates = new()
        {
            ["WeeklyOn"] = "Weekly on {0}",
            ["MonthlyOnThe"] = "Monthly on the {0}",
            ["EveryNWeeksOn"] = "Every {0} weeks on {1}",
            ["EveryNMonthsOnThe"] = "Every {0} months on the {1}",
            ["UseOrdinalSuffixes"] = "true",
            ["WeekdayMonday"] = "Monday",
            ["WeekdayTuesday"] = "Tuesday",
            ["WeekdayWednesday"] = "Wednesday",
            ["WeekdayThursday"] = "Thursday",
            ["WeekdayFriday"] = "Friday",
            ["WeekdaySaturday"] = "Saturday",
            ["WeekdaySunday"] = "Sunday",
        };

        public LocalizedString this[string name] => new(name, Templates[name]);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(Templates[name], arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        public IStringLocalizer WithCulture(CultureInfo? culture) => this;
    }

    private static readonly FakeLocalizer Loc = new();
    private static RecurrenceEvaluator.RecurrenceSpec Weekly(int weekdays, DateOnly anchor, DateOnly? end = null) =>
        new(RecurrenceMode.Weekly, weekdays, null, null, anchor, end);

    private static RecurrenceEvaluator.RecurrenceSpec Monthly(int dayOfMonth, DateOnly anchor, DateOnly? end = null) =>
        new(RecurrenceMode.Monthly, 0, dayOfMonth, null, anchor, end);

    private static RecurrenceEvaluator.RecurrenceSpec EveryNWeeks(int interval, DateOnly anchor, DateOnly? end = null) =>
        new(RecurrenceMode.EveryNWeeks, 0, null, interval, anchor, end);

    private static RecurrenceEvaluator.RecurrenceSpec EveryNMonths(int interval, DateOnly anchor, DateOnly? end = null) =>
        new(RecurrenceMode.EveryNMonths, 0, null, interval, anchor, end);

    [Fact]
    public void Weekly_SingleWeekday_ReturnsAllOccurrencesAcrossTwoWeeks()
    {
        // 2026-01-05 is a Monday. Window covers Mon Jan 5 – Sun Jan 18 (two full weeks).
        var spec = Weekly(0b000001, new DateOnly(2026, 1, 1)); // Monday bit
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 5), new(2026, 1, 18));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 5),
            new(2026, 1, 12),
        };
        Assert.Equal(expected, occurrences);
    }

    [Fact]
    public void Weekly_MultiDayMonWedFri_ReturnsAllOccurrencesInMonth()
    {
        // Jan 2026: Thu Jan 1, Fri Jan 2 ... January 1, 2026 is a Thursday.
        var weekdays = (1 << 0) | (1 << 2) | (1 << 4); // Mon=1, Wed=4, Fri=16
        var spec = Weekly(weekdays, new DateOnly(2026, 1, 1));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 31));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 2), // Fri
            new(2026, 1, 5), // Mon
            new(2026, 1, 7), // Wed
            new(2026, 1, 9), // Fri
            new(2026, 1, 12), // Mon
            new(2026, 1, 14), // Wed
            new(2026, 1, 16), // Fri
            new(2026, 1, 19), // Mon
            new(2026, 1, 21), // Wed
            new(2026, 1, 23), // Fri
            new(2026, 1, 26), // Mon
            new(2026, 1, 28), // Wed
            new(2026, 1, 30), // Fri
        };
        Assert.Equal(expected, occurrences);
    }

    [Fact]
    public void Monthly_Day31_NonLeap2026_ClampsToFeb28()
    {
        var spec = Monthly(31, new DateOnly(2026, 1, 31));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 2, 1), new(2026, 2, 28));

        Assert.Equal([new DateOnly(2026, 2, 28)], occurrences);
    }

    [Fact]
    public void Monthly_Day31_Leap2028_ClampsToFeb29()
    {
        var spec = Monthly(31, new DateOnly(2028, 1, 31));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2028, 2, 1), new(2028, 2, 29));

        Assert.Equal([new DateOnly(2028, 2, 29)], occurrences);
    }

    [Fact]
    public void Monthly_Day31_ClampsToApr30()
    {
        var spec = Monthly(31, new DateOnly(2026, 1, 31));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 4, 1), new(2026, 4, 30));

        Assert.Equal([new DateOnly(2026, 4, 30)], occurrences);
    }

    [Fact]
    public void EveryNMonths_Interval1_FromJan31_DoesNotRecoverTo31st()
    {
        // Documented deviation: stepping from the LAST occurrence, not recomputed from the anchor.
        var spec = EveryNMonths(1, new DateOnly(2026, 1, 31));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 31), new(2026, 4, 30));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 31),
            new(2026, 2, 28),
            new(2026, 3, 28),
            new(2026, 4, 28),
        };
        Assert.Equal(expected, occurrences);
    }

    [Fact]
    public void EveryNWeeks_Interval2_StepsExactly14Days()
    {
        var spec = EveryNWeeks(2, new DateOnly(2026, 1, 5)); // Monday
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 5), new(2026, 2, 14));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 5),
            new(2026, 1, 19),
            new(2026, 2, 2),
        };
        Assert.Equal(expected, occurrences);
        for (var i = 1; i < occurrences.Count; i++)
        {
            Assert.Equal(14, occurrences[i].DayNumber - occurrences[i - 1].DayNumber);
        }
    }

    [Fact]
    public void EveryNWeeks_FirstOccurrenceIsAnchorItself()
    {
        var spec = EveryNWeeks(2, new DateOnly(2026, 1, 5));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 5), new(2026, 3, 31));

        Assert.Equal(new DateOnly(2026, 1, 5), occurrences[0]);
    }

    [Fact]
    public void EveryNWeeks_Interval2_MidIntervalWindowStart_IncludesNextGridMultiple()
    {
        // Anchor Jan 1 2026 (Thursday). Occurrences are Jan 1 + 7k, stepping by 2 weeks:
        // Jan 1, 15, 29... Window starts mid-interval on Jan 9, so the first in-window
        // multiple is Jan 15 (not Jan 22 — weekday math must stay anchored to Jan 1).
        var spec = EveryNWeeks(2, new DateOnly(2026, 1, 1));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 9), new(2026, 1, 31));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 15),
            new(2026, 1, 29),
        };
        Assert.Equal(expected, occurrences);
    }

    [Fact]
    public void EveryNWeeks_WindowStartsExactlyOnBoundaryMultiple_IncludesBoundary()
    {
        // Jan 15 2026 is exactly 2 weeks from the Jan 1 anchor — it must be included.
        var spec = EveryNWeeks(2, new DateOnly(2026, 1, 1));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 15), new(2026, 1, 31));

        Assert.Equal([new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 29)], occurrences);
    }

    [Fact]
    public void EveryNWeeks_Interval1_MidWeekWindowStart_LandsOnAnchorWeekday()
    {
        // Anchor Jan 1 (Thursday), interval 1, window starts mid-week on Jan 6 (Tuesday).
        // First occurrence is the next Thursday, Jan 8, then Jan 15 — never Jan 7.
        var spec = EveryNWeeks(1, new DateOnly(2026, 1, 1));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 6), new(2026, 1, 20));

        Assert.Equal([new DateOnly(2026, 1, 8), new DateOnly(2026, 1, 15)], occurrences);
    }

    [Fact]
    public void EveryNMonths_FarPastAnchor_Interval3_FastForwardsToRecentWindow()
    {
        // Anchor 2024-09-05, interval 3: 2024-09-05, 2024-12-05, 2025-03-05, 2025-06-05,
        // 2025-09-05, 2025-12-05, 2026-03-05, 2026-06-05, 2026-09-05...
        // Only 2026-09-05 falls in [2026-08-01, 2026-09-30]. Day 5 < any month length,
        // so no clamping is involved on this path.
        var spec = EveryNMonths(3, new DateOnly(2024, 9, 5));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 8, 1), new(2026, 9, 30));

        Assert.Equal([new DateOnly(2026, 9, 5)], occurrences);
    }

    [Fact]
    public void EndDate_Inclusive_ReturnsOccurrenceOnEndDate()
    {
        var spec = Weekly(0b000001, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 12)); // Mondays
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 31));

        var expected = new List<DateOnly>
        {
            new(2026, 1, 5),
            new(2026, 1, 12), // exactly on EndDate
        };
        Assert.Equal(expected, occurrences);
    }

    [Fact]
    public void EndDate_ExcludesOccurrenceAfterIt()
    {
        var spec = Weekly(0b000001, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11)); // Mondays, ends before Jan 12
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 31));

        Assert.Equal([new DateOnly(2026, 1, 5)], occurrences);
    }

    [Fact]
    public void Weekly_Monthly_NoOccurrenceBeforeAnchorDate()
    {
        // Anchor is Jan 10 but window starts Jan 1 — early occurrences must be excluded.
        var weeklySpec = Weekly(0b000001, new DateOnly(2026, 1, 10)); // Mondays
        var weekly = RecurrenceEvaluator.GetOccurrences(weeklySpec, new(2026, 1, 1), new(2026, 1, 31));
        Assert.All(weekly, d => Assert.True(d >= new DateOnly(2026, 1, 10)));
        Assert.Equal([new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 19), new DateOnly(2026, 1, 26)], weekly);

        var monthlySpec = Monthly(15, new DateOnly(2026, 2, 1));
        var monthly = RecurrenceEvaluator.GetOccurrences(monthlySpec, new(2026, 1, 1), new(2026, 3, 31));
        Assert.All(monthly, d => Assert.True(d >= new DateOnly(2026, 2, 1)));
        Assert.Equal([new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 15)], monthly);
    }

    [Fact]
    public void AnchorDateInFuture_ReturnsEmptyForTodayWindow()
    {
        var spec = Weekly(0b1111111, new DateOnly(2030, 1, 1));
        var occurrences = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 31));

        Assert.Empty(occurrences);
    }

    [Fact]
    public void AdjacentWindows_NoDoubleCountOrSkippedBoundary()
    {
        // Daily-ish spec: all 7 weekdays → every day occurs.
        var spec = Weekly(0b1111111, new DateOnly(2026, 1, 1));
        var first = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 15));
        var second = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 16), new(2026, 1, 31));
        var combined = RecurrenceEvaluator.GetOccurrences(spec, new(2026, 1, 1), new(2026, 1, 31));

        var concatenated = first.Concat(second).ToList();
        Assert.Equal(combined, combined.Distinct().ToList()); // no duplicates
        Assert.Equal(combined, concatenated); // order preserved
        Assert.Equal(combined.Count, combined.Distinct().Count());
        Assert.Equal(concatenated.Count, combined.Distinct().Count());
        Assert.Equal(combined.Count, combined.Distinct().Count());
        Assert.Equal(31, combined.Count); // Jan 1–31 = 31 days, every day is an occurrence
    }

    [Fact]
    public void Describe_ReturnsCanonicalStrings()
    {
        Assert.Equal(
            "Weekly on Monday, Wednesday",
            RecurrenceEvaluator.Describe(Weekly((1 << 0) | (1 << 2), new DateOnly(2026, 1, 1)), Loc));
        Assert.Equal(
            "Monthly on the 15th",
            RecurrenceEvaluator.Describe(Monthly(15, new DateOnly(2026, 1, 1)), Loc));
        Assert.Equal(
            "Every 2 weeks on Monday",
            RecurrenceEvaluator.Describe(EveryNWeeks(2, new DateOnly(2026, 1, 5)), Loc)); // Monday
        Assert.Equal(
            "Every 3 months on the 28th",
            RecurrenceEvaluator.Describe(EveryNMonths(3, new DateOnly(2026, 1, 28)), Loc));
    }

    [Fact]
    public void GetDescribeParts_ReturnsCultureIndependentParts()
    {
        // Weekly: all selected weekday bits, no day/interval
        var weekly = RecurrenceEvaluator.GetDescribeParts(Weekly((1 << 0) | (1 << 2), new DateOnly(2026, 1, 1)));
        Assert.Equal("WeeklyOn", weekly.TemplateKey);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], weekly.Weekdays);
        Assert.Null(weekly.DayOfMonth);
        Assert.Null(weekly.Interval);

        // Monthly: day-of-month only
        var monthly = RecurrenceEvaluator.GetDescribeParts(Monthly(15, new DateOnly(2026, 1, 1)));
        Assert.Equal("MonthlyOnThe", monthly.TemplateKey);
        Assert.Empty(monthly.Weekdays);
        Assert.Equal(15, monthly.DayOfMonth);
        Assert.Null(monthly.Interval);

        // EveryNWeeks: anchor weekday + interval
        var nWeeks = RecurrenceEvaluator.GetDescribeParts(EveryNWeeks(2, new DateOnly(2026, 1, 5))); // Monday
        Assert.Equal("EveryNWeeksOn", nWeeks.TemplateKey);
        Assert.Equal([DayOfWeek.Monday], nWeeks.Weekdays);
        Assert.Null(nWeeks.DayOfMonth);
        Assert.Equal(2, nWeeks.Interval);

        // EveryNMonths: anchor day-of-month + interval
        var nMonths = RecurrenceEvaluator.GetDescribeParts(EveryNMonths(3, new DateOnly(2026, 1, 28)));
        Assert.Equal("EveryNMonthsOnThe", nMonths.TemplateKey);
        Assert.Empty(nMonths.Weekdays);
        Assert.Equal(28, nMonths.DayOfMonth);
        Assert.Equal(3, nMonths.Interval);
    }

    [Fact]
    public void InvalidSpecs_ThrowArgumentException()
    {
        var weekly = Weekly(0, new DateOnly(2026, 1, 1)); // no weekday bits
        var monthlyNoDay = new RecurrenceEvaluator.RecurrenceSpec(RecurrenceMode.Monthly, 0, null, null, new DateOnly(2026, 1, 1), null);
        var monthlyBadDay = Monthly(32, new DateOnly(2026, 1, 1));
        var nWeeksNoInterval = new RecurrenceEvaluator.RecurrenceSpec(RecurrenceMode.EveryNWeeks, 0, null, null, new DateOnly(2026, 1, 1), null);
        var nWeeksZeroInterval = EveryNWeeks(0, new DateOnly(2026, 1, 1));
        var nMonthsZeroInterval = EveryNMonths(0, new DateOnly(2026, 1, 1));

        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(weekly, new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(monthlyNoDay, new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(monthlyBadDay, new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(nWeeksNoInterval, new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(nWeeksZeroInterval, new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.GetOccurrences(nMonthsZeroInterval, new(2026, 1, 1), new(2026, 1, 31)));

        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.Describe(weekly, Loc));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.Describe(monthlyNoDay, Loc));
        Assert.Throws<ArgumentException>(() => RecurrenceEvaluator.Describe(nWeeksZeroInterval, Loc));
    }
}