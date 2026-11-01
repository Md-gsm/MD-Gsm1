using System.Collections.Concurrent;
using Xunit;
using MDGsm.Persons.Domain;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PartyIdTests
{
    [Fact]
    public void New_CreatesUuidVersionSeven()
    {
        var partyId = PartyId.New();

        Assert.NotEqual(Guid.Empty, partyId.Value);
        Assert.True(PartyId.TryParse(partyId.ToString(), out var parsed));
        Assert.Equal(partyId, parsed);
    }

    [Fact]
    public void From_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => PartyId.From(Guid.Empty));
    }

    [Fact]
    public void From_RejectsNonVersionSevenGuid()
    {
        var nonVersionSeven = Guid.Parse("11111111-1111-4111-8111-111111111111");

        Assert.Throws<ArgumentException>(() => PartyId.From(nonVersionSeven));
    }

    [Fact]
    public void From_RejectsUuidVersionSevenWithNonRfcVariant()
    {
        var invalidVariant = Guid.Parse("018f0000-0000-7000-0000-000000000000");

        Assert.Throws<ArgumentException>(() => PartyId.From(invalidVariant));
    }

    [Fact]
    public void TryParse_RejectsNonCanonicalGuidFormats()
    {
        var value = Guid.Parse("018f0000-0000-7000-8000-000000000001");
        var alternatives = new[]
        {
            value.ToString("N"),
            value.ToString("B"),
            value.ToString("P")
        };

        foreach (var alternative in alternatives)
        {
            Assert.False(PartyId.TryParse(alternative, out _));
        }

        Assert.True(PartyId.TryParse(value.ToString("D"), out _));
        Assert.False(PartyId.TryParse(value.ToString("D").ToUpperInvariant(), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData(" 018f0000-0000-7000-8000-000000000001")]
    [InlineData("018f0000-0000-7000-8000-000000000001 ")]
    public void TryParse_RejectsMalformedOrWhitespacePaddedValues(string value)
    {
        Assert.False(PartyId.TryParse(value, out _));
    }

    [Fact]
    public void TryParse_RejectsNull()
    {
        Assert.False(PartyId.TryParse(null, out _));
    }

    [Fact]
    public void Parse_RejectsNull()
    {
        Assert.Throws<FormatException>(() => PartyId.Parse(null!));
    }

    [Fact]
    public void Parse_RejectsNonCanonicalGuidFormat()
    {
        const string value = "018f0000-0000-7000-8000-000000000001";

        Assert.Throws<FormatException>(() => PartyId.Parse(Guid.Parse(value).ToString("N")));
        Assert.Throws<FormatException>(() => PartyId.Parse(value.ToUpperInvariant()));
    }

    [Fact]
    public void Parse_RejectsWhitespacePaddedGuid()
    {
        Assert.Throws<FormatException>(() => PartyId.Parse(" 018f0000-0000-7000-8000-000000000001 "));
    }

    [Fact]
    public void New_GeneratesUniqueIdsUnderConcurrency()
    {
        var generated = new ConcurrentDictionary<Guid, byte>();

        Parallel.For(0, 4096, _ =>
        {
            var id = PartyId.New();
            Assert.True(generated.TryAdd(id.Value, 0), "PartyId collision detected during concurrent generation.");
        });

        Assert.Equal(4096, generated.Count);
    }

    [Fact]
    public void Parse_RoundTripsCanonicalUuidString()
    {
        var original = PartyId.New();

        var parsed = PartyId.Parse(original.ToString());

        Assert.Equal(original, parsed);
        Assert.Equal(original.ToString(), parsed.ToString());
    }

}
