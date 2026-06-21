using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using ClassCast.Common.Logging;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Result of an Active Directory authentication attempt.
/// </summary>
/// <param name="Success">Whether authentication (and group membership) succeeded.</param>
/// <param name="Message">A human-readable description suitable for display.</param>
/// <param name="DisplayName">The authenticated user's display name, when available.</param>
public readonly record struct AuthResult(bool Success, string Message, string? DisplayName);

/// <summary>
/// Validates teacher credentials against Active Directory and enforces membership
/// of the configured teacher group (specification section 3.1). Uses
/// <c>System.DirectoryServices.AccountManagement</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AdAuthenticator
{
    private readonly string _domain;
    private readonly string _teacherGroup;

    /// <summary>
    /// Initialises the authenticator.
    /// </summary>
    /// <param name="domain">The Active Directory domain (NetBIOS short name).</param>
    /// <param name="teacherGroup">The group whose members may use the Teacher Server.</param>
    public AdAuthenticator(string domain, string teacherGroup)
    {
        _domain = domain;
        _teacherGroup = teacherGroup;
    }

    /// <summary>
    /// Validates the supplied credentials and, when present, the user's membership of
    /// the teacher group. If the group does not exist the check fails open
    /// (any valid AD user is allowed), as required for initial deployment.
    /// </summary>
    /// <param name="username">The sAMAccountName to validate.</param>
    /// <param name="password">The account password.</param>
    /// <returns>An <see cref="AuthResult"/> describing the outcome.</returns>
    public AuthResult Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return new AuthResult(false, "Username and password are required.", null);
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain, _domain);

            if (!context.ValidateCredentials(username, password))
            {
                Logger.Warn($"AD validation failed for '{username}'.");
                return new AuthResult(false, "Invalid username or password.", null);
            }

            using UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
            if (user is null)
            {
                return new AuthResult(false, "Authenticated, but the user account could not be located.", null);
            }

            if (!IsInTeacherGroup(context, user))
            {
                Logger.Warn($"User '{username}' is not a member of '{_teacherGroup}'.");
                return new AuthResult(false, $"You are not a member of the '{_teacherGroup}' group.", null);
            }

            string display = string.IsNullOrWhiteSpace(user.DisplayName) ? username : user.DisplayName!;
            Logger.Info($"Teacher '{username}' authenticated successfully.");
            return new AuthResult(true, "Authenticated.", display);
        }
        catch (PrincipalException ex)
        {
            Logger.Error("AD authentication error.", ex);
            return new AuthResult(false, $"Active Directory error: {ex.Message}", null);
        }
        catch (Exception ex)
        {
            Logger.Error("Unexpected authentication error.", ex);
            return new AuthResult(false, $"Authentication error: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Checks whether the user belongs to the teacher group. If the group cannot be
    /// found, the check fails open and returns <c>true</c> (any valid AD user allowed).
    /// </summary>
    private bool IsInTeacherGroup(PrincipalContext context, UserPrincipal user)
    {
        try
        {
            using GroupPrincipal? group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, _teacherGroup);
            if (group is null)
            {
                Logger.Warn($"Teacher group '{_teacherGroup}' not found; failing open (allowing any valid AD user).");
                return true;
            }
            return user.IsMemberOf(group);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Group membership check failed ({ex.Message}); failing open.");
            return true;
        }
    }
}
