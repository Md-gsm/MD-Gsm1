using System.Globalization;
using System.Text;

namespace MDGsm.IdentityAccess.Domain;

/// <summary>Stable account identifier based on UUID version 7.</summary>
public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.CreateVersion7());

    public static AccountId From(Guid value) =>
        value == Guid.Empty ? throw new ArgumentException("Account ID cannot be empty.", nameof(value)) : new(value);
}

/// <summary>
/// Login name value object. NFC and invariant uppercase are the v1 key algorithm.
/// Full Unicode Script_Extensions and CaseFolding table pinning remain a release gate.
/// </summary>
public sealed record Username
{
    public const int NormalizationVersion = 1;
    public string DisplayValue { get; }
    public string NormalizedValue { get; }

    private Username(string displayValue, string normalizedValue)
    {
        DisplayValue = displayValue;
        NormalizedValue = normalizedValue;
    }

    public static Username Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var value = input.Normalize(NormalizationForm.FormC);
        var scalarCount = 0;
        var scriptGroup = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            scalarCount++;
            var category = Rune.GetUnicodeCategory(rune);
            if (category is not (UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber))
                throw new FormatException("Username accepts only Unicode letters and decimal digits.");

            var currentGroup = GetScriptGroup(rune);
            if (currentGroup != 0 && scriptGroup != 0 && currentGroup != scriptGroup)
                throw new FormatException("Mixed writing systems are not allowed.");
            if (currentGroup != 0) scriptGroup = currentGroup;
        }

        if (scalarCount is < 3 or > 64)
            throw new FormatException("Username must contain 3 to 64 Unicode scalar values.");

        return new Username(value, value.ToUpperInvariant());
    }

    private static int GetScriptGroup(Rune rune)
    {
        var n = rune.Value;
        if ((n >= 0x0041 && n <= 0x024F) || (n >= 0x1E00 && n <= 0x1EFF)) return 1;
        if ((n >= 0x0600 && n <= 0x06FF) || (n >= 0x0750 && n <= 0x077F)
            || (n >= 0x08A0 && n <= 0x08FF) || (n >= 0xFB50 && n <= 0xFDFF)
            || (n >= 0xFE70 && n <= 0xFEFF)) return 2;
        if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber) return 0;
        return 3;
    }
}

public sealed class Account
{
    public const int FailedAttemptLimit = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public AccountId Id { get; }
    public Username Username { get; }
    public string PasswordHash { get; private set; }
    public short PasswordHashVersion { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public bool MustChangePassword { get; private set; }
    public long CredentialVersion { get; private set; } = 1;
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? FailureWindowStartedAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    public Account(AccountId id, Username username, string passwordHash, short passwordHashVersion, bool mustChangePassword = false)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (passwordHashVersion < 1) throw new ArgumentOutOfRangeException(nameof(passwordHashVersion));
        Id = id;
        Username = username ?? throw new ArgumentNullException(nameof(username));
        PasswordHash = passwordHash;
        PasswordHashVersion = passwordHashVersion;
        MustChangePassword = mustChangePassword;
    }

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public void RecordFailedLogin(DateTimeOffset now)
    {
        if (FailureWindowStartedAt is null || now - FailureWindowStartedAt > FailureWindow)
        {
            FailureWindowStartedAt = now;
            FailedAttemptCount = 0;
        }
        FailedAttemptCount++;
        if (FailedAttemptCount >= FailedAttemptLimit) LockedUntil = now.Add(LockDuration);
    }

    public void RecordSuccessfulLogin()
    {
        FailedAttemptCount = 0;
        FailureWindowStartedAt = null;
        LockedUntil = null;
    }

    public void ChangePassword(string passwordHash, short hashVersion, bool mustChangePassword = false)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (hashVersion < 1) throw new ArgumentOutOfRangeException(nameof(hashVersion));
        PasswordHash = passwordHash;
        PasswordHashVersion = hashVersion;
        MustChangePassword = mustChangePassword;
        CredentialVersion = checked(CredentialVersion + 1);
        RecordSuccessfulLogin();
    }

    public void CompleteRequiredPasswordChange() => MustChangePassword = false;
    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

public sealed record SessionPolicy(int MaxActiveUsers, TimeSpan HeartbeatInterval, TimeSpan LeaseDuration)
{
    public static SessionPolicy Initial => new(5, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(180));

    public SessionPolicy Validate()
    {
        if (MaxActiveUsers < 1) throw new ArgumentOutOfRangeException(nameof(MaxActiveUsers));
        if (HeartbeatInterval <= TimeSpan.Zero || LeaseDuration <= HeartbeatInterval)
            throw new ArgumentException("Lease duration must exceed a positive heartbeat interval.");
        return this;
    }
}

public static class RoleCatalog
{
    public const string SuperAdmin = "super_admin";
    public const string Manager = "manager";
    public const string Receptionist = "receptionist";
    public const string Technician = "technician";
    public const string Warehouse = "warehouse";
    public const string Salesperson = "salesperson";
    public const string Purchaser = "purchaser";
    public const string Cashier = "cashier";
    public const string Accountant = "accountant";
    public const string Viewer = "viewer";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin, Manager, Receptionist, Technician, Warehouse,
        Salesperson, Purchaser, Cashier, Accountant, Viewer
    };
}
