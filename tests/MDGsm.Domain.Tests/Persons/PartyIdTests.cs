using MDGsm.Domain.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PartyIdTests
{
    [Fact]
    public void New_generates_non_empty_uuid_v7_with_rfc_variant()
    {
        var id = PartyId.New();
        var canonical = id.Value.ToString("N");

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal('7', canonical[12]);
        Assert.Contains(canonical[16], new[] { '8', '9', 'a', 'b' });
    }

    [Fact]
    public void New_generates_unique_values()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => PartyId.New()).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void FromGuid_rejects_non_v7_and_empty_values()
    {
        Assert.Throws<ArgumentException>(() => PartyId.FromGuid(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => PartyId.FromGuid(Guid.Empty));
    }

    [Fact]
    public void TryParse_accepts_uuid_v7_and_rejects_other_values()
    {
        var expected = PartyId.New();

        Assert.True(PartyId.TryParse(expected.ToString(), out var actual));
        Assert.Equal(expected, actual);
        Assert.False(PartyId.TryParse(Guid.NewGuid().ToString(), out _));
        Assert.False(PartyId.TryParse("not-a-guid", out _));
        Assert.False(PartyId.TryParse(null, out _));
    }

    [Fact]
    public void Equal_values_have_value_equality()
    {
        var original = PartyId.New();
        var reconstructed = PartyId.FromGuid(original.Value);

        Assert.Equal(original, reconstructed);
    }
}
