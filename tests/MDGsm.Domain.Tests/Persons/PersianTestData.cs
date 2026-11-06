namespace MDGsm.Domain.Tests.Persons;

/// <summary>
/// Persian test strings written as explicit code points so that Arabic keyboard variants
/// (U+064A yeh, U+0643 kaf) and their Persian canonical forms (U+06CC, U+06A9) cannot be mixed up by an editor.
/// </summary>
internal static class PersianTestData
{
    /// <summary>"علي" typed with Arabic yeh.</summary>
    public const string AliArabicYeh = "علي";

    /// <summary>"رضايي" typed with Arabic yeh.</summary>
    public const string RezaeiArabicYeh = "رضايي";

    /// <summary>"علی رضایی" in canonical Persian form.</summary>
    public const string AliRezaeiCanonical = "علی رضایی";

    /// <summary>"كريم" typed with Arabic kaf and yeh.</summary>
    public const string KarimArabic = "كريم";

    /// <summary>"کریم" in canonical Persian form.</summary>
    public const string KarimCanonical = "کریم";

    /// <summary>"شركت كيان" typed with Arabic variants.</summary>
    public const string CompanyArabic = "شركت كیان";

    /// <summary>"شرکت کیان" in canonical Persian form.</summary>
    public const string CompanyCanonical = "شرکت کیان";

    /// <summary>"می‌خواهم" with ZWNJ (must survive normalization).</summary>
    public const string WithZwnj = "می‌خواهم";

    /// <summary>"محمد" with tatweel between letters.</summary>
    public const string MohammadWithTatweel = "مـحـمـد";

    public const string MohammadCanonical = "محمد";
}
