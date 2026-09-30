using Diva.Api.Features.Common;

namespace Diva.Tests.Features;

public class LikePatternTests
{
    [Theory]
    [InlineData("budi", "%budi%")]
    [InlineData("100%", @"%100\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\x", @"%c:\\x%")]
    public void Wildcards_in_the_search_term_are_escaped(string term, string expected)
    {
        Assert.Equal(expected, LikePattern.Contains(term));
    }
}
