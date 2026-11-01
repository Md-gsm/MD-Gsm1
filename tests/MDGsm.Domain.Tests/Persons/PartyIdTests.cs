using System.Collections.Concurrent;
using System.Diagnostics;
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
        var value = PartyId.New().Value;
        var alternatives = new[]
        {
            value.ToString("N"),
            value.ToString("B"),
            value.ToString("P"),
            value.ToString("D").ToUpperInvariant()
        };

        foreach (var alternative in alternatives)
        {
            Assert.False(PartyId.TryParse(alternative, out _));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    public void TryParse_RejectsMalformedValues(string value)
    {
        Assert.False(PartyId.TryParse(value, out _));
    }

    [Fact]
    public void Parse_RejectsNonCanonicalGuidFormat()
    {
        var value = PartyId.New().Value.ToString("N");

        Assert.Throws<FormatException>(() => PartyId.Parse(value));
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
    public void New_EncodesIncreasingTimestampsAcrossDistinctMilliseconds()
    {
        var first = PartyId.New();
        var firstTimestamp = GetUuidTimestamp(first.Value);
        var timer = Stopwatch.StartNew();
        PartyId? later = null;

        while (timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            var candidate = PartyId.New();
            if (GetUuidTimestamp(candidate.Value) > firstTimestamp)
            {
                later = candidate;
                break;
            }

            Thread.Sleep(1);
        }

        Assert.NotNull(later);
        Assert.True(GetUuidTimestamp(later.Value) > firstTimestamp);
    }

    [Fact]
    public void Parse_RoundTripsCanonicalUuidString()
    {
        var original = PartyId.New();

        var parsed = PartyId.Parse(original.ToString());

        Assert.Equal(original, parsed);
        Assert.Equal(original.ToString(), parsed.ToString());
    }

    private static long GetUuidTimestamp(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        Assert.True(value.TryWriteBytes(bytes, bigEndian: true, out var written));
        Assert.Equal(16, written);

        long timestamp = 0;
        for (var index = 0; index < 6; index++)
        {
            timestamp = (timestamp << 8) | bytes[index];
        }

        return timestamp;
    }
}
