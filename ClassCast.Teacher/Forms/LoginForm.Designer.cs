#nullable disable
namespace ClassCast.Teacher.Forms;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>Clean up any resources being used.</summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new System.Windows.Forms.Label();
        this.lblDomain = new System.Windows.Forms.Label();
        this.txtDomain = new System.Windows.Forms.TextBox();
        this.lblUser = new System.Windows.Forms.Label();
        this.txtUser = new System.Windows.Forms.TextBox();
        this.lblPassword = new System.Windows.Forms.Label();
        this.txtPassword = new System.Windows.Forms.TextBox();
        this.btnLogin = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.lblError = new System.Windows.Forms.Label();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
        this.lblTitle.Location = new System.Drawing.Point(24, 18);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new System.Drawing.Size(320, 30);
        this.lblTitle.Text = "ClassCast Teacher Sign-in";
        //
        // lblDomain
        //
        this.lblDomain.Location = new System.Drawing.Point(24, 64);
        this.lblDomain.Name = "lblDomain";
        this.lblDomain.Size = new System.Drawing.Size(90, 23);
        this.lblDomain.Text = "Domain";
        this.lblDomain.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // txtDomain
        //
        this.txtDomain.Location = new System.Drawing.Point(120, 62);
        this.txtDomain.Name = "txtDomain";
        this.txtDomain.Size = new System.Drawing.Size(224, 27);
        //
        // lblUser
        //
        this.lblUser.Location = new System.Drawing.Point(24, 100);
        this.lblUser.Name = "lblUser";
        this.lblUser.Size = new System.Drawing.Size(90, 23);
        this.lblUser.Text = "Username";
        this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // txtUser
        //
        this.txtUser.Location = new System.Drawing.Point(120, 98);
        this.txtUser.Name = "txtUser";
        this.txtUser.Size = new System.Drawing.Size(224, 27);
        //
        // lblPassword
        //
        this.lblPassword.Location = new System.Drawing.Point(24, 136);
        this.lblPassword.Name = "lblPassword";
        this.lblPassword.Size = new System.Drawing.Size(90, 23);
        this.lblPassword.Text = "Password";
        this.lblPassword.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // txtPassword
        //
        this.txtPassword.Location = new System.Drawing.Point(120, 134);
        this.txtPassword.Name = "txtPassword";
        this.txtPassword.Size = new System.Drawing.Size(224, 27);
        this.txtPassword.UseSystemPasswordChar = true;
        //
        // lblError
        //
        this.lblError.ForeColor = System.Drawing.Color.Firebrick;
        this.lblError.Location = new System.Drawing.Point(24, 168);
        this.lblError.Name = "lblError";
        this.lblError.Size = new System.Drawing.Size(320, 36);
        this.lblError.Text = "";
        //
        // btnLogin
        //
        this.btnLogin.Location = new System.Drawing.Point(190, 210);
        this.btnLogin.Name = "btnLogin";
        this.btnLogin.Size = new System.Drawing.Size(72, 30);
        this.btnLogin.Text = "Sign in";
        this.btnLogin.UseVisualStyleBackColor = true;
        this.btnLogin.Click += new System.EventHandler(this.OnLoginClick);
        //
        // btnCancel
        //
        this.btnCancel.Location = new System.Drawing.Point(272, 210);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(72, 30);
        this.btnCancel.Text = "Exit";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.OnCancelClick);
        //
        // LoginForm
        //
        this.AcceptButton = this.btnLogin;
        this.CancelButton = this.btnCancel;
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.ClientSize = new System.Drawing.Size(368, 258);
        this.Controls.Add(this.lblTitle);
        this.Controls.Add(this.lblDomain);
        this.Controls.Add(this.txtDomain);
        this.Controls.Add(this.lblUser);
        this.Controls.Add(this.txtUser);
        this.Controls.Add(this.lblPassword);
        this.Controls.Add(this.txtPassword);
        this.Controls.Add(this.lblError);
        this.Controls.Add(this.btnLogin);
        this.Controls.Add(this.btnCancel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "LoginForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "ClassCast Teacher";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblDomain;
    private System.Windows.Forms.TextBox txtDomain;
    private System.Windows.Forms.Label lblUser;
    private System.Windows.Forms.TextBox txtUser;
    private System.Windows.Forms.Label lblPassword;
    private System.Windows.Forms.TextBox txtPassword;
    private System.Windows.Forms.Label lblError;
    private System.Windows.Forms.Button btnLogin;
    private System.Windows.Forms.Button btnCancel;
}
