using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;

namespace ClassCast.Student.Services;

/// <summary>
/// Guards the tray "Exit" action so a student cannot simply close the client. The
/// user is prompted for a local administrator password or an Active Directory
/// credential, which is validated before the application is allowed to quit
/// (specification sections 5.1, 12).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ExitGuard
{
    /// <summary>
    /// Prompts for and validates an administrator/AD credential.
    /// </summary>
    /// <param name="owner">The dialog owner window.</param>
    /// <param name="config">Configuration providing the AD domain to try.</param>
    /// <returns><c>true</c> if a valid credential was supplied; otherwise <c>false</c>.</returns>
    public static bool AuthoriseExit(IWin32Window? owner, AppConfig config)
    {
        using var prompt = new CredentialPrompt();
        if (prompt.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        if (Validate(prompt.UserName, prompt.Password, config))
        {
            Logger.Info($"Exit authorised by '{prompt.UserName}'.");
            return true;
        }

        MessageBox.Show(owner,
            "The supplied credentials are not valid for a local administrator or domain account.",
            "ClassCast", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    /// <summary>
    /// Validates the credential as either a local administrator or a valid AD account.
    /// </summary>
    private static bool Validate(string user, string password, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        // 1. Local administrator?
        try
        {
            using var machine = new PrincipalContext(ContextType.Machine);
            if (machine.ValidateCredentials(user, password) && IsLocalAdministrator(machine, user))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Local credential check failed: {ex.Message}");
        }

        // 2. Any valid Active Directory account on the configured domain.
        try
        {
            using var domain = new PrincipalContext(ContextType.Domain, config.AdDomain);
            if (domain.ValidateCredentials(user, password))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Domain credential check failed: {ex.Message}");
        }

        return false;
    }

    /// <summary>Checks whether the user is a member of the local Administrators group.</summary>
    private static bool IsLocalAdministrator(PrincipalContext machine, string user)
    {
        try
        {
            using UserPrincipal? principal = UserPrincipal.FindByIdentity(machine, IdentityType.SamAccountName, user);
            if (principal is null)
            {
                return false;
            }

            // Well-known SID S-1-5-32-544 = BUILTIN\Administrators.
            using var admins = GroupPrincipal.FindByIdentity(machine, IdentityType.Sid, "S-1-5-32-544");
            return admins is not null && principal.IsMemberOf(admins);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Administrator membership check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>A small modal dialog collecting a username and password.</summary>
    private sealed class CredentialPrompt : Form
    {
        private readonly TextBox _user = new() { Left = 110, Top = 18, Width = 200 };
        private readonly TextBox _password = new() { Left = 110, Top = 54, Width = 200, UseSystemPasswordChar = true };

        /// <summary>Gets the entered username.</summary>
        public string UserName => _user.Text.Trim();

        /// <summary>Gets the entered password.</summary>
        public string Password => _password.Text;

        /// <summary>Builds the credential prompt dialog.</summary>
        public CredentialPrompt()
        {
            Text = "ClassCast - administrator required";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(330, 140);

            var lblUser = new Label { Left = 16, Top = 21, Width = 90, Text = "Username" };
            var lblPass = new Label { Left = 16, Top = 57, Width = 90, Text = "Password" };
            var ok = new Button { Text = "OK", Left = 150, Top = 96, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 235, Top = 96, Width = 75, DialogResult = DialogResult.Cancel };

            Controls.AddRange([lblUser, _user, lblPass, _password, ok, cancel]);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
