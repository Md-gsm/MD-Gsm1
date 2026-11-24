using System.Collections.Concurrent;
using System.Globalization;
using MDGsm.Domain.Persons;
using MDGsm.IdentityAccess.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class UuidV7IdentifierConformanceTests
{
    [Fact]
    public void AccountId_new_uses_rfc9562_uuid_v7_and_rfc_variant()
    {
        var id = AccountId.New();
        AssertUuidV7(id.Value);
    }

    [Fact]
    public void PartyId_new_uses_rfc9562_uuid_v7_and_rfc_variant()
    {
        var id = PartyId.New();
        AssertUuidV7(id.Value);
    }

    [Fact]
    public void Guid_version7_timestamp_overload_encodes_unix_milliseconds_and_randomizes_same_millisecond_ids()
    {
        var supplied = new DateTimeOffset(2026, 11, 22, 19, 30, 0, TimeSpan.FromHours(3.5));
        var expectedMilliseconds = supplied.ToUnixTimeMilliseconds();
        var ids = Enumerable.Range(0, 2_048)
            .Select(_ => Guid.CreateVersion7(supplied))
            .ToArray();

        Assert.All(ids, id =>
        {
            AssertUuidV7(id);
            var canonical = id.ToString("N", CultureInfo.InvariantCulture);
            Assert.Equal(expectedMilliseconds.ToString("x12", CultureInfo.InvariantCulture), canonical[..12]);
        });

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Guid_version7_timestamp_overload_rejects_pre_unix_epoch_values()
    {
        var beforeEpoch = DateTimeOffset.UnixEpoch.AddTicks(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Guid.CreateVersion7(beforeEpoch));
    }

    [Fact]
    public void AccountId_generation_remains_unique_under_parallel_calls()
    {
        const int count = 20_000;
        var ids = new ConcurrentBag<AccountId>();

        Parallel.For(0, count, _ => ids.Add(AccountId.New()));

        Assert.Equal(count, ids.Count);
        Assert.Equal(count, ids.Distinct().Count());
        Assert.All(ids, id => AssertUuidV7(id.Value));
    }

    private static void AssertUuidV7(Guid value)
    {
        Assert.NotEqual(Guid.Empty, value);
        var canonical = value.ToString("N", CultureInfo.InvariantCulture);
        Assert.Equal('7', canonical[12]);
        Assert.Contains(canonical[16], new[] { '8', '9', 'a', 'b' });
    }
}
