using System.Numerics;
using System.Text;

namespace MDGsm.Accounting.Domain;

public readonly record struct IrrAmount(long Value)
{
    public static IrrAmount Zero => new(0);
    public static IrrAmount operator +(IrrAmount left, IrrAmount right) => new(checked(left.Value + right.Value));
    public static IrrAmount operator -(IrrAmount left, IrrAmount right) => new(checked(left.Value - right.Value));
    public static IrrAmount Multiply(IrrAmount amount, long quantity) => new(checked(amount.Value * quantity));
    public static IrrAmount FromRatio(BigInteger numerator, BigInteger denominator) => new(RoundAwayFromZero(numerator, denominator));

    public static long RoundAwayFromZero(BigInteger numerator, BigInteger denominator)
    {
        if (denominator <= BigInteger.Zero)
            throw new ArgumentOutOfRangeException(nameof(denominator), "Denominator must be positive.");

        var absolute = BigInteger.Abs(numerator);
        var quotient = BigInteger.DivRem(absolute, denominator, out var remainder);
        if (remainder >= denominator - remainder)
            quotient += BigInteger.One;

        if (numerator.Sign < 0)
            quotient = BigInteger.Negate(quotient);

        return ToInt64Checked(quotient);
    }

    public static IReadOnlyList<IrrAllocation> AllocateLargestRemainder(
        IrrAmount amount,
        IEnumerable<IrrAllocationWeight> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var input = weights.ToArray();
        if (input.Length == 0)
            throw new ArgumentException("At least one allocation weight is required.", nameof(weights));

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        BigInteger totalWeight = BigInteger.Zero;
        foreach (var item in input)
        {
            if (string.IsNullOrWhiteSpace(item.LineId) || item.LineId != item.LineId.Trim())
                throw new ArgumentException("Allocation line identifiers must be non-empty and canonical.", nameof(weights));
            if (!identifiers.Add(item.LineId))
                throw new ArgumentException("Allocation line identifiers must be unique.", nameof(weights));
            if (item.Weight < 0)
                throw new ArgumentOutOfRangeException(nameof(weights), "Allocation weights cannot be negative.");
            totalWeight += item.Weight;
        }

        if (totalWeight <= BigInteger.Zero)
            throw new ArgumentException("The sum of allocation weights must be positive.", nameof(weights));

        var absoluteAmount = BigInteger.Abs(new BigInteger(amount.Value));
        var shares = input.Select(item =>
        {
            var product = absoluteAmount * item.Weight;
            var baseShare = BigInteger.DivRem(product, totalWeight, out var remainder);
            return new Share(item.LineId, baseShare, remainder);
        }).ToArray();

        var baseTotal = shares.Aggregate(BigInteger.Zero, (sum, share) => sum + share.BaseShare);
        var remainingCount = checked((int)(absoluteAmount - baseTotal));
        var recipients = shares
            .OrderByDescending(share => share.Remainder)
            .ThenBy(share => share.LineId, Utf8OrdinalComparer.Instance)
            .Take(remainingCount)
            .Select(share => share.LineId)
            .ToHashSet(StringComparer.Ordinal);

        var sign = amount.Value < 0 ? BigInteger.MinusOne : BigInteger.One;
        return shares
            .Select(share =>
            {
                var allocated = share.BaseShare + (recipients.Contains(share.LineId) ? BigInteger.One : BigInteger.Zero);
                return new IrrAllocation(share.LineId, new IrrAmount(ToInt64Checked(allocated * sign)));
            })
            .OrderBy(allocation => allocation.LineId, Utf8OrdinalComparer.Instance)
            .ToArray();
    }

    private static long ToInt64Checked(BigInteger value)
    {
        if (value < long.MinValue || value > long.MaxValue)
            throw new OverflowException("The resulting IRR amount is outside the Int64 storage range.");
        return (long)value;
    }

    private sealed record Share(string LineId, BigInteger BaseShare, BigInteger Remainder);

    private sealed class Utf8OrdinalComparer : IComparer<string>
    {
        public static Utf8OrdinalComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            var length = Math.Min(leftBytes.Length, rightBytes.Length);
            for (var index = 0; index < length; index++)
            {
                var comparison = leftBytes[index].CompareTo(rightBytes[index]);
                if (comparison != 0) return comparison;
            }
            return leftBytes.Length.CompareTo(rightBytes.Length);
        }
    }
}

public sealed record IrrAllocationWeight(string LineId, long Weight);
public sealed record IrrAllocation(string LineId, IrrAmount Amount);
