using SplitDuo.Api.Features.Users.Validation;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class PersonNameRulesTests
{
    [Theory]
    [InlineData("Alice")]
    [InlineData("José")]
    [InlineData("李")]
    [InlineData("O'Brien")]
    [InlineData("Anne-Marie")]
    [InlineData("Mary Jane")]
    [InlineData("St. John")]
    [InlineData("Łukasz")]
    [InlineData("田中・太郎")]
    public void IsValid_RealNames_Accepted(string name)
        => Assert.True(PersonNameRules.IsValid(name));

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("Alice & Bob")]
    [InlineData("Alice\u0000Bob")]
    [InlineData("Alice\u202EBob")] // bidi override
    [InlineData("Alice 123")]
    public void IsValid_MarkupOrControlCharacters_Rejected(string name)
        => Assert.False(PersonNameRules.IsValid(name));

    [Fact]
    public void IsValid_LoneSurrogate_Rejected()
    {
        // System.Text.Json can deserialize an unpaired surrogate; Normalize throws.
        var name = "Alice" + '\uD800' + "Bob";
        Assert.False(PersonNameRules.IsValid(name));
    }

    [Fact]
    public void IsValid_TooLong_Rejected()
        => Assert.False(PersonNameRules.IsValid(new string('a', 101)));

    [Fact]
    public void IsValid_Empty_AllowedForOptionalName()
        => Assert.True(PersonNameRules.IsValid(""));

    [Fact]
    public void Normalize_TrimsCollapsesWhitespaceAndComposes()
        => Assert.Equal("Mary Jane", PersonNameRules.Normalize("  Mary   Jane  "));

    [Fact]
    public void Normalize_ComposesCombiningMarks()
        => Assert.Equal("José", PersonNameRules.Normalize("Jose\u0301"));
}
