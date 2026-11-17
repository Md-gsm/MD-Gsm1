from __future__ import annotations

import unittest
from fractions import Fraction

MIN64, MAX64 = -(2**63), 2**63 - 1


def _require_integer(value: object, name: str) -> int:
    # bool is an int subclass in Python, but it is never a valid monetary input.
    if type(value) is not int:
        raise TypeError(f"{name} must be an integer")
    return value


def require_irr(value: object) -> int:
    amount = _require_integer(value, "IRR amount")
    if amount < MIN64 or amount > MAX64:
        raise OverflowError("IRR amount outside Int64 range")
    return amount


def round_away_with_error(
    numerator: int, denominator: int, lo: int = MIN64, hi: int = MAX64
) -> tuple[int, Fraction]:
    numerator = _require_integer(numerator, "numerator")
    denominator = _require_integer(denominator, "denominator")
    lo = _require_integer(lo, "lower bound")
    hi = _require_integer(hi, "upper bound")
    if denominator <= 0:
        raise ValueError("denominator must be positive")
    if lo > hi:
        raise ValueError("invalid target range")

    # Python int is the unbounded-integer reference for .NET BigInteger.
    magnitude = abs(numerator)
    quotient, remainder = divmod(magnitude, denominator)
    # Avoid the overflow-prone 2 * remainder comparison.
    if remainder >= denominator - remainder:
        quotient += 1

    rounded = -quotient if numerator < 0 else quotient
    if rounded < lo or rounded > hi:
        raise OverflowError("result outside target integer range")

    # Exact, non-monetary control metric. It is never persisted or posted as money.
    signed_error = Fraction(rounded * denominator - numerator, denominator)
    return rounded, signed_error


def round_away(numerator: int, denominator: int, lo: int = MIN64, hi: int = MAX64) -> int:
    return round_away_with_error(numerator, denominator, lo, hi)[0]


def multiply_irr_by_integer(amount: int, quantity: int) -> int:
    amount = require_irr(amount)
    quantity = _require_integer(quantity, "quantity")
    result = amount * quantity
    if result < MIN64 or result > MAX64:
        raise OverflowError("multiplication result outside Int64 range")
    return result


def allocate(amount: int, rows: list[tuple[str, int]]) -> dict[str, int]:
    amount = require_irr(amount)
    if not rows:
        raise ValueError("at least one allocation row is required")

    ids: list[str] = []
    for key, weight in rows:
        if not isinstance(key, str) or not key:
            raise ValueError("row IDs must be non-empty stable strings")
        _require_integer(weight, "weight")
        if weight < 0:
            raise ValueError("weights must be non-negative")
        ids.append(key)

    if len(ids) != len(set(ids)):
        raise ValueError("row IDs must be unique")

    total_weight = sum(weight for _, weight in rows)
    if total_weight <= 0:
        raise ValueError("sum of weights must be positive")

    magnitude = abs(amount)
    result: dict[str, int] = {}
    remainders: list[tuple[int, bytes, str]] = []
    used = 0

    for key, weight in rows:
        base, remainder = divmod(magnitude * weight, total_weight)
        result[key] = base
        used += base
        remainders.append((remainder, key.encode("utf-8"), key))

    residual_units = magnitude - used
    if residual_units < 0 or residual_units > len(rows):
        raise AssertionError("largest-remainder conservation invariant failed")

    # Stable bytewise UTF-8 tie-break; independent of locale and input order.
    ranked = sorted(remainders, key=lambda item: (-item[0], item[1]))
    for _, _, key in ranked[:residual_units]:
        result[key] += 1

    sign = -1 if amount < 0 else 1
    allocated = {key: sign * value for key, value in result.items()}
    if sum(allocated.values()) != amount:
        raise AssertionError("allocation must conserve the exact integer amount")
    if any(value < MIN64 or value > MAX64 for value in allocated.values()):
        raise OverflowError("allocated row amount outside Int64 range")
    return allocated


def document_rounding_budget(operations: list[tuple[int, int]]) -> Fraction:
    # Net document effect: sum signed operation residuals exactly. Fraction is
    # a non-monetary control metric only, never an amount stored or posted.
    budget = Fraction(0, 1)
    for numerator, denominator in operations:
        _, error = round_away_with_error(numerator, denominator)
        budget += error
    if abs(budget) > 1:
        raise ValueError("document rounding budget exceeds 1 IRR")
    return budget


class IntegerRoundingReferenceTests(unittest.TestCase):
    def test_away_from_zero_vectors(self):
        vectors = [
            (1249, 100, 12), (1250, 100, 13), (1251, 100, 13),
            (-1249, 100, -12), (-1250, 100, -13), (-1251, 100, -13),
            (49, 100, 0), (50, 100, 1), (-50, 100, -1),
            (250, 100, 3), (-250, 100, -3), (0, 7, 0),
            (MAX64 - 1, MAX64, 1),
        ]
        for numerator, denominator, expected in vectors:
            with self.subTest(numerator=numerator, denominator=denominator):
                self.assertEqual(expected, round_away(numerator, denominator))

    def test_int64_min_max_and_midpoints(self):
        self.assertEqual(MIN64, round_away(MIN64, 1))
        self.assertEqual(MAX64, round_away(MAX64, 1))
        self.assertEqual(-4611686018427387904, round_away(MIN64, 2))
        self.assertEqual(4611686018427387904, round_away(MAX64, 2))

    def test_wide_intermediate_and_checked_narrowing(self):
        self.assertEqual(4611686018427387904, round_away(MAX64 * 100, 200))
        with self.assertRaises(OverflowError):
            round_away(MAX64 + 1, 1)
        with self.assertRaises(OverflowError):
            round_away(MIN64 - 1, 1)

    def test_invalid_denominators_and_bounds(self):
        for denominator in (0, -1, -100):
            with self.subTest(denominator=denominator):
                with self.assertRaises(ValueError):
                    round_away(1, denominator)
        with self.assertRaises(ValueError):
            round_away(1, 1, 10, 0)

    def test_money_inputs_are_integer_irr_only(self):
        self.assertEqual(125, require_irr(125))
        for value in (125.0, Fraction(125, 1), "125.0", True, None):
            with self.subTest(value=value):
                with self.assertRaises(TypeError):
                    require_irr(value)
        for value in (MAX64 + 1, MIN64 - 1):
            with self.subTest(value=value):
                with self.assertRaises(OverflowError):
                    require_irr(value)

    def test_integer_amount_times_integer_quantity(self):
        self.assertEqual(3000, multiply_irr_by_integer(1000, 3))
        self.assertEqual(-3000, multiply_irr_by_integer(1000, -3))
        with self.assertRaises(OverflowError):
            multiply_irr_by_integer(MAX64, 2)
        with self.assertRaises(TypeError):
            multiply_irr_by_integer(100, 1.5)

    def test_deterministic_exact_weighted_allocation(self):
        expected = {"A": 34, "B": 33, "C": 33}
        self.assertEqual(expected, allocate(100, [("A", 1), ("B", 1), ("C", 1)]))
        self.assertEqual(expected, allocate(100, [("C", 1), ("A", 1), ("B", 1)]))
        self.assertEqual({key: -value for key, value in expected.items()},
                         allocate(-100, [("A", 1), ("B", 1), ("C", 1)]))
        weighted = allocate(101, [("A", 1), ("B", 2), ("C", 3)])
        self.assertEqual({"A": 17, "B": 34, "C": 50}, weighted)
        self.assertEqual(101, sum(weighted.values()))

    def test_allocation_invalid_inputs(self):
        invalid_rows = [
            [],
            [("", 1)],
            [("A", 0), ("B", 0)],
            [("A", -1), ("B", 2)],
            [("A", 1), ("A", 2)],
            [("A", 1.0)],
            [(None, 1)],
        ]
        for rows in invalid_rows:
            with self.subTest(rows=rows):
                with self.assertRaises((TypeError, ValueError)):
                    allocate(10, rows)  # type: ignore[arg-type]
        with self.assertRaises(TypeError):
            allocate(1.0, [("A", 1)])  # type: ignore[arg-type]

    def test_rounding_budget_uses_signed_net_document_effect(self):
        self.assertEqual(Fraction(1, 1), document_rounding_budget([(1, 2), (1, 2)]))
        self.assertEqual(Fraction(0, 1), document_rounding_budget([(1, 2), (-1, 2)]))
        self.assertEqual(Fraction(0, 1), document_rounding_budget([(200, 100), (-300, 100)]))

    def test_rounding_budget_rejects_more_than_one_irr(self):
        with self.assertRaises(ValueError):
            document_rounding_budget([(1, 2), (1, 2), (1, 2)])

    def test_rounding_error_is_exact_and_not_a_money_value(self):
        rounded, error = round_away_with_error(1250, 100)
        self.assertEqual(13, rounded)
        self.assertEqual(Fraction(1, 2), error)
        rounded_negative, negative_error = round_away_with_error(-1250, 100)
        self.assertEqual(-13, rounded_negative)
        self.assertEqual(Fraction(-1, 2), negative_error)


if __name__ == "__main__":
    unittest.main(verbosity=2)
