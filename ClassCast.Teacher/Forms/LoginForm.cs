using System.Runtime.Versioning;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Teacher.Services;

namespace ClassCast.Teacher.Forms;

/// <summary>
/// Modal login dialog shown at startup. Validates teacher credentials against
/// Active Directory via <see cref="AdAuthenticator"/> and enforces the maximum of
/// five consecutive failures, after which the application exits
/// (specification section 3.1).
/// </summary>
[SupportedOSPlatform("windows")]
public partial class LoginForm : Form
{
    private const int MaxFailures = 5;

    private readonly AppConfig _config;
    private readonly AdAuthenticator _authenticator;
    private int _failureCount;

    /// <summary>Gets the display name of the authenticated teacher after a successful login.</summary>
    public string AuthenticatedTeacher { get; private set; } = string.Empty;

    /// <summary>
    /// Initialises the login dialog.
    /// </summary>
    /// <param name="config">Application configuration (domain, teacher group).</param>
    public LoginForm(AppConfig config)
    {
        _config = config;
        _authenticator = new AdAuthenticator(config.AdDomain, config.AdTeacherGroup);
        InitializeComponent();
        Icon = ClassCast.Common.AppIcon.Load();
        txtDomain.Text = config.AdDomain;
        txtUser.Focus();
    }

    /// <summary>Validates the entered credentials when the Sign in button is clicked.</summary>
    private void OnLoginClick(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            AuthResult result = _authenticator.Authenticate(txtUser.Text.Trim(), txtPassword.Text);
            if (result.Success)
            {
                AuthenticatedTeacher = result.DisplayName ?? txtUser.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _failureCount++;
            lblError.Text = $"{result.Message} (attempt {_failureCount} of {MaxFailures})";
            txtPassword.Clear();
            txtPassword.Focus();

            if (_failureCount >= MaxFailures)
            {
                Logger.Error("Maximum login attempts exceeded; exiting.");
                MessageBox.Show(this,
                    "Maximum number of sign-in attempts exceeded. The application will now close.",
                    "ClassCast", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Abort;
                Close();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Closes the dialog (and the application) when Exit is clicked.</summary>
    private void OnCancelClick(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>Enables or disables inputs during the (synchronous) authentication call.</summary>
    private void SetBusy(bool busy)
    {
        btnLogin.Enabled = !busy;
        txtUser.Enabled = !busy;
        txtPassword.Enabled = !busy;
        txtDomain.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        if (busy)
        {
            lblError.Text = "Checking credentials...";
        }
        Update();
    }
}
