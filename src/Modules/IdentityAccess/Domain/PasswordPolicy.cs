namespace MDGsm.IdentityAccess.Domain;

/// <summary>
/// Shared server-side password length policy for account password changes and resets.
/// This centralizes the accepted 12..128 UTF-16 code-unit rule without adding
/// composition or character-class requirements.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static bool IsValid(string? password) =>
        password is { Length: >= MinimumLength and <= MaximumLength };

    public static bool IsValidChange(string? currentPassword, string? newPassword) =>
        IsValid(newPassword)
        && !string.Equals(currentPassword, newPassword, StringComparison.Ordinal);
}
