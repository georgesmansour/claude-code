using InvitationPlatform.Api.Services;

namespace InvitationPlatform.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("John Doe", "john-doe")]
    [InlineData("  John   Doe  ", "john-doe")]
    [InlineData("Jane Doe", "jane-doe")]
    [InlineData("O'Brien & Sons", "o-brien-sons")]
    [InlineData("Charbel Nahhas", "charbel-nahhas")]
    public void Slugify_produces_lowercase_hyphenated_ascii(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Theory]
    [InlineData("José Muñoz", "jose-munoz")]      // accents stripped
    [InlineData("Zoë Renée", "zoe-renee")]
    [InlineData("François Léger", "francois-leger")]
    public void Slugify_removes_diacritics(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Slugify(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData(null)]
    public void Slugify_falls_back_to_guest_when_nothing_usable(string? input)
    {
        Assert.Equal("guest", SlugHelper.Slugify(input));
    }

    [Fact]
    public void Slugify_is_stable_across_case_and_punctuation()
    {
        // Cosmetic differences must map to the same base slug (drives the "don't churn the link" rule).
        Assert.Equal(SlugHelper.Slugify("John Doe"), SlugHelper.Slugify("JOHN  DOE!"));
    }

    [Fact]
    public void RandomSuffix_is_lowercase_alphanumeric_of_requested_length()
    {
        var s = SlugHelper.RandomSuffix(6);
        Assert.Equal(6, s.Length);
        Assert.Matches("^[a-z0-9]+$", s);
    }
}
