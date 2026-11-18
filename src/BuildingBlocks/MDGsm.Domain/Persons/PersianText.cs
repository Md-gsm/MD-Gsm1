using System.Text;

namespace MDGsm.Domain.Persons;

/// <summary>
/// Canonical form for Persian free text used in names and addresses, so that the same
/// name typed with Arabic keyboard variants is stored and searched identically.
/// Digits are intentionally left untouched (see <see cref="PartyIdentifiers.NormalizeDigits"/>).
/// </summary>
public static class PersianText
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var composed = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(composed.Length);
        var pendingSpace = false;

        foreach (var original in composed)
        {
            if (IsIgnorable(original)) continue;

            if (char.IsWhiteSpace(original))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(original switch
            {
                'ي' or 'ى' => 'ی', // Arabic yeh / alef maksura -> Persian yeh
                'ك' => 'ک',             // Arabic kaf -> Persian keheh
                _ => original
            });
        }

        return builder.ToString();
    }

    // Tatweel, Arabic harakat, bidi marks, zero-width space and BOM carry no identity.
    // ZWNJ (U+200C) is meaningful in Persian and is preserved.
    private static bool IsIgnorable(char c) =>
        c is 'ـ' or '​' or '‎' or '‏' or '﻿'
        || (c >= 'ً' && c <= 'ْ') || c == 'ٰ';
}
