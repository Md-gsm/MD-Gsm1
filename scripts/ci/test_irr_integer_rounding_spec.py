from __future__ import annotations
import unittest

MIN64, MAX64 = -(2**63), 2**63 - 1

def round_away(n: int, d: int, lo: int = MIN64, hi: int = MAX64) -> int:
    n, d = int(n), int(d)
    if d <= 0:
        raise ValueError("denominator must be positive")
    q, r = divmod(abs(n), d)  # Python int is the unbounded-integer reference for .NET BigInteger.
    if r >= d - r:            # Avoids the overflow-prone 2*r comparison.
        q += 1
    result = -q if n < 0 else q
    if result < lo or result > hi:
        raise OverflowError("result outside target integer range")
    return result

def allocate(amount: int, rows: list[tuple[str, int]]) -> dict[str, int]:
    ids = [key for key, _ in rows]
    if not rows or len(ids) != len(set(ids)) or any(w < 0 for _, w in rows):
        raise ValueError("rows must have unique IDs and non-negative weights")
    total_weight = sum(w for _, w in rows)
    if total_weight <= 0:
        raise ValueError("sum of weights must be positive")
    magnitude = abs(amount)
    result, remainders = {}, []
    used = 0
    for key, weight in rows:
        base, rem = divmod(magnitude * weight, total_weight)
        result[key] = base
        used += base
        remainders.append((rem, key))
    for _, key in sorted(remainders, key=lambda x: (-x[0], x[1]))[:magnitude-used]:
        result[key] += 1
    sign = -1 if amount < 0 else 1
    return {key: sign * value for key, value in result.items()}

def check_document_delta(delta: int) -> None:
    if abs(delta) > 1:
        raise ValueError("document rounding delta exceeds 1 IRR")

class IntegerRoundingReferenceTests(unittest.TestCase):
    def test_away_from_zero_vectors(self):
        for n, d, expected in [
            (1249,100,12),(1250,100,13),(1251,100,13),
            (-1249,100,-12),(-1250,100,-13),(-1251,100,-13),
            (49,100,0),(50,100,1),(-50,100,-1),(250,100,3),(-250,100,-3),(0,7,0)
        ]:
            with self.subTest(n=n,d=d):
                self.assertEqual(expected, round_away(n,d))

    def test_int64_min_max_and_midpoints(self):
        self.assertEqual(MIN64, round_away(MIN64,1))
        self.assertEqual(MAX64, round_away(MAX64,1))
        self.assertEqual(-4611686018427387904, round_away(MIN64,2))
        self.assertEqual(4611686018427387904, round_away(MAX64,2))

    def test_wide_intermediate_and_checked_narrowing(self):
        self.assertEqual(4611686018427387904, round_away(MAX64*100,200))
        with self.assertRaises(OverflowError): round_away(MAX64+1,1)
        with self.assertRaises(OverflowError): round_away(MIN64-1,1)

    def test_invalid_denominators(self):
        for d in (0,-1,-100):
            with self.assertRaises(ValueError): round_away(1,d)

    def test_deterministic_exact_weighted_allocation(self):
        expected={"A":34,"B":33,"C":33}
        self.assertEqual(expected,allocate(100,[("A",1),("B",1),("C",1)]))
        self.assertEqual(expected,allocate(100,[("C",1),("A",1),("B",1)]))
        self.assertEqual({k:-v for k,v in expected.items()},allocate(-100,[("A",1),("B",1),("C",1)]))
        weighted=allocate(101,[("A",1),("B",2),("C",3)])
        self.assertEqual({"A":17,"B":34,"C":50},weighted)
        self.assertEqual(101,sum(weighted.values()))

    def test_invalid_allocation_inputs(self):
        for rows in ([],[("A",0),("B",0)],[("A",-1),("B",2)],[("A",1),("A",2)]):
            with self.assertRaises(ValueError): allocate(10,rows)

    def test_one_irr_document_cap(self):
        for delta in (-1,0,1): check_document_delta(delta)
        for delta in (-2,2):
            with self.assertRaises(ValueError): check_document_delta(delta)

if __name__ == "__main__":
    unittest.main(verbosity=2)
