using SplitDuo.Api.Features.System.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class VersionFeedParserTests
{
    #region Docker Hub tags parsing

    [Fact]
    public void ParseDockerHubTags_HighestSemverWins()
    {
        const string json = """
            {
              "count": 3,
              "results": [
                { "name": "1.2.0" },
                { "name": "1.10.0" },
                { "name": "1.9.5" }
              ]
            }
            """;

        var result = VersionFeedParser.ParseDockerHubTags(json);
        Assert.Equal(new SemanticVersion(1, 10, 0), result);
    }

    [Fact]
    public void ParseDockerHubTags_LatestTagIgnored()
    {
        const string json = """
            {
              "results": [
                { "name": "latest" },
                { "name": "2.0.1" }
              ]
            }
            """;

        var result = VersionFeedParser.ParseDockerHubTags(json);
        Assert.Equal(new SemanticVersion(2, 0, 1), result);
    }

    [Fact]
    public void ParseDockerHubTags_NonSemverTagsIgnored()
    {
        const string json = """
            {
              "results": [
                { "name": "latest" },
                { "name": "sha-abc123" },
                { "name": "main" },
                { "name": "3.4.5" }
              ]
            }
            """;

        var result = VersionFeedParser.ParseDockerHubTags(json);
        Assert.Equal(new SemanticVersion(3, 4, 5), result);
    }

    [Fact]
    public void ParseDockerHubTags_MalformedJson_ReturnsNull()
    {
        Assert.Null(VersionFeedParser.ParseDockerHubTags("{ not json !!"));
    }

    [Fact]
    public void ParseDockerHubTags_EmptyResults_ReturnsNull()
    {
        const string json = """{ "count": 0, "results": [] }""";
        Assert.Null(VersionFeedParser.ParseDockerHubTags(json));
    }

    [Fact]
    public void ParseDockerHubTags_NoValidSemverTags_ReturnsNull()
    {
        const string json = """{ "results": [ { "name": "latest" }, { "name": "edge" } ] }""";
        Assert.Null(VersionFeedParser.ParseDockerHubTags(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseDockerHubTags_NullOrWhitespace_ReturnsNull(string? json)
    {
        Assert.Null(VersionFeedParser.ParseDockerHubTags(json));
    }

    [Fact]
    public void ParseDockerHubTags_MissingResultsKey_ReturnsNull()
    {
        const string json = """{ "count": 0 }""";
        Assert.Null(VersionFeedParser.ParseDockerHubTags(json));
    }

    #endregion

    #region TryParseNextLink

    [Fact]
    public void TryParseNextLink_ValidLink_ReturnsTrue()
    {
        var json = """{"next": "https://hub.docker.com/v2/repositories/j1mm0/splitduo/tags?page=2&page_size=100", "results": []}""";

        Assert.True(VersionFeedParser.TryParseNextLink(json, out var next));
        Assert.Equal("https://hub.docker.com/v2/repositories/j1mm0/splitduo/tags?page=2&page_size=100", next);
    }

    [Theory]
    [InlineData("""{"next": null, "results": []}""")]            // last page
    [InlineData("""{"results": []}""")]                          // missing
    [InlineData("""{"next": 42, "results": []}""")]             // non-string
    [InlineData("""{"next": "https://evil.example.com/tags?page=2"}""")] // off-host
    [InlineData("""{"next": "http://hub.docker.com/tags?page=2"}""")]    // not https
    [InlineData("""{"next": "/v2/repositories/tags?page=2"}""")]         // relative
    [InlineData("not json")]                                     // malformed
    [InlineData(null)]                                           // null input
    public void TryParseNextLink_Invalid_ReturnsFalse(string? json)
    {
        Assert.False(VersionFeedParser.TryParseNextLink(json, out var next));
        Assert.Null(next);
    }

    #endregion

    #region VERSION file parsing

    [Fact]
    public void ParseVersionFile_ValidContent_ReturnsVersion()
    {
        var result = VersionFeedParser.ParseVersionFile("1.16.0\n");
        Assert.Equal(new SemanticVersion(1, 16, 0), result);
    }

    [Fact]
    public void ParseVersionFile_ValidContent_NoNewline_ReturnsVersion()
    {
        var result = VersionFeedParser.ParseVersionFile("2.3.4");
        Assert.Equal(new SemanticVersion(2, 3, 4), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("1.2")]
    public void ParseVersionFile_Malformed_ReturnsNull(string? content)
    {
        Assert.Null(VersionFeedParser.ParseVersionFile(content));
    }

    #endregion
}