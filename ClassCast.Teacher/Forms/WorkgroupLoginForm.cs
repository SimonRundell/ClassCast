using System.Runtime.Versioning;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Security;

namespace ClassCast.Teacher.Forms;

/// <summary>
/// Modal login dialog used when the Teacher Server runs in workgroup (non-domain) mode.
/// Instead of Active Directory it validates a single shared teacher password held as a
/// salted hash in <c>config.json</c>. If no password has been set yet (the hash is empty),
/// the dialog first prompts to create one and saves it. As with the AD dialog, five
/// consecutive failures close the application.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WorkgroupLoginForm : Form
{
    private const int MaxFailures = 5;
    private const int MinPasswordLength = 6;

    private readonly AppConfig _config;
    private readonly bool _setMode;
    private int _failureCount;

    private readonly Label _lblTitle = new();
    private readonly Label _lblPrompt = new();
    private readonly Label _lblPassword = new();
    private readonly TextBox _txtPassword = new();
    private readonly Label _lblConfirm = new();
    private readonly TextBox _txtConfirm = new();
    private readonly Label _lblError = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    /// <summary>Gets a friendly name for the signed-in teacher after a successful login.</summary>
    public string AuthenticatedTeacher { get; private set; } = string.Empty;

    /// <summary>
    /// Initialises the dialog, choosing between "set a new password" and "enter the
    /// password" depending on whether a hash already exists in configuration.
    /// </summary>
    /// <param name="config">Application configuration carrying the stored password hash.</param>
    public WorkgroupLoginForm(AppConfig config)
    {
        _config = config;
        _setMode = string.IsNullOrWhiteSpace(config.TeacherPasswordHash);
        BuildLayout();
    }

    /// <summary>Builds the dialog controls in code (no designer file).</summary>
    private void BuildLayout()
    {
        SuspendLayout();

        _lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
        _lblTitle.Location = new System.Drawing.Point(24, 18);
        _lblTitle.Size = new System.Drawing.Size(320, 30);
        _lblTitle.Text = "ClassCast Teacher";

        _lblPrompt.Location = new System.Drawing.Point(24, 56);
        _lblPrompt.Size = new System.Drawing.Size(320, 40);
        _lblPrompt.Text = _setMode
            ? "No teacher password is set on this machine. Create one to protect the Teacher console."
            : "Enter the teacher password to start the Teacher console.";

        _lblPassword.Location = new System.Drawing.Point(24, 104);
        _lblPassword.Size = new System.Drawing.Size(90, 23);
        _lblPassword.Text = _setMode ? "New password" : "Password";
        _lblPassword.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        _txtPassword.Location = new System.Drawing.Point(140, 102);
        _txtPassword.Size = new System.Drawing.Size(204, 27);
        _txtPassword.UseSystemPasswordChar = true;

        // The confirm field is only shown when creating a new password.
        _lblConfirm.Location = new System.Drawing.Point(24, 140);
        _lblConfirm.Size = new System.Drawing.Size(90, 23);
        _lblConfirm.Text = "Confirm";
        _lblConfirm.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        _lblConfirm.Visible = _setMode;

        _txtConfirm.Location = new System.Drawing.Point(140, 138);
        _txtConfirm.Size = new System.Drawing.Size(204, 27);
        _txtConfirm.UseSystemPasswordChar = true;
        _txtConfirm.Visible = _setMode;

        int errorTop = _setMode ? 174 : 138;
        _lblError.ForeColor = System.Drawing.Color.Firebrick;
        _lblError.Location = new System.Drawing.Point(24, errorTop);
        _lblError.Size = new System.Drawing.Size(320, 36);
        _lblError.Text = "";

        int buttonTop = errorTop + 42;
        _btnOk.Location = new System.Drawing.Point(168, buttonTop);
        _btnOk.Size = new System.Drawing.Size(94, 30);
        _btnOk.Text = _setMode ? "Set & start" : "Sign in";
        _btnOk.UseVisualStyleBackColor = true;
        _btnOk.Click += OnOkClick;

        _btnCancel.Location = new System.Drawing.Point(272, buttonTop);
        _btnCancel.Size = new System.Drawing.Size(72, 30);
        _btnCancel.Text = "Exit";
        _btnCancel.UseVisualStyleBackColor = true;
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new System.Drawing.Size(368, buttonTop + 48);
        Controls.AddRange(new Control[]
        {
            _lblTitle, _lblPrompt, _lblPassword, _txtPassword,
            _lblConfirm, _txtConfirm, _lblError, _btnOk, _btnCancel
        });
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ClassCast Teacher";
        Icon = ClassCast.Common.AppIcon.Load();

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>Handles the OK button for both the set-password and sign-in flows.</summary>
    private void OnOkClick(object? sender, EventArgs e)
    {
        if (_setMode)
        {
            CreatePassword();
        }
        else
        {
            VerifyPassword();
        }
    }

    /// <summary>Validates, hashes and stores a brand-new teacher password.</summary>
    private void CreatePassword()
    {
        string password = _txtPassword.Text;
        if (password.Length < MinPasswordLength)
        {
            _lblError.Text = $"Password must be at least {MinPasswordLength} characters.";
            return;
        }
        if (password != _txtConfirm.Text)
        {
            _lblError.Text = "The passwords do not match.";
            _txtConfirm.Clear();
            _txtConfirm.Focus();
            return;
        }

        try
        {
            _config.TeacherPasswordHash = PasswordHasher.Hash(password);
            _config.Save();
        }
        catch (Exception ex)
        {
            // Most likely the app lacks write access to its Program Files folder.
            Logger.Error("Could not save the teacher password.", ex);
            _lblError.Text = "Could not save the password (administrator rights may be " +
                             "needed). Re-run the installer to set it.";
            return;
        }

        Logger.Info("Workgroup teacher password set.");
        Succeed();
    }

    /// <summary>Verifies the entered password against the stored hash.</summary>
    private void VerifyPassword()
    {
        if (PasswordHasher.Verify(_txtPassword.Text, _config.TeacherPasswordHash))
        {
            Logger.Info("Workgroup teacher authenticated successfully.");
            Succeed();
            return;
        }

        _failureCount++;
        _lblError.Text = $"Incorrect password (attempt {_failureCount} of {MaxFailures}).";
        _txtPassword.Clear();
        _txtPassword.Focus();

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

    /// <summary>Records the signed-in teacher and closes the dialog with success.</summary>
    private void Succeed()
    {
        AuthenticatedTeacher = Environment.UserName;
        DialogResult = DialogResult.OK;
        Close();
    }
}
