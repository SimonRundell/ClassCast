#nullable disable
namespace ClassCast.Teacher.Forms;

partial class StudentTile
{
    /// <summary>Required designer variable.</summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>Clean up any resources being used.</summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _thumbnail?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.picThumb = new System.Windows.Forms.PictureBox();
        this.lblName = new System.Windows.Forms.Label();
        this.lblStatus = new System.Windows.Forms.Label();
        this.lblLock = new System.Windows.Forms.Label();
        this.btnLock = new System.Windows.Forms.Button();
        this.btnLogoff = new System.Windows.Forms.Button();
        ((System.ComponentModel.ISupportInitialize)(this.picThumb)).BeginInit();
        this.SuspendLayout();
        //
        // picThumb
        //
        this.picThumb.BackColor = System.Drawing.Color.Black;
        this.picThumb.Location = new System.Drawing.Point(8, 28);
        this.picThumb.Name = "picThumb";
        this.picThumb.Size = new System.Drawing.Size(320, 180);
        this.picThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.picThumb.TabStop = false;
        //
        // lblName
        //
        this.lblName.AutoEllipsis = true;
        this.lblName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.lblName.Location = new System.Drawing.Point(8, 6);
        this.lblName.Name = "lblName";
        this.lblName.Size = new System.Drawing.Size(320, 18);
        this.lblName.Text = "(waiting)";
        //
        // lblStatus
        //
        this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 8F);
        this.lblStatus.ForeColor = System.Drawing.Color.DimGray;
        this.lblStatus.Location = new System.Drawing.Point(8, 212);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(150, 26);
        this.lblStatus.Text = "Connected";
        //
        // lblLock (padlock badge shown over the thumbnail when the student is locked)
        //
        this.lblLock.BackColor = System.Drawing.Color.Firebrick;
        this.lblLock.Font = new System.Drawing.Font("Segoe UI Emoji", 11F);
        this.lblLock.ForeColor = System.Drawing.Color.White;
        this.lblLock.Location = new System.Drawing.Point(300, 32);
        this.lblLock.Name = "lblLock";
        this.lblLock.Size = new System.Drawing.Size(24, 24);
        this.lblLock.Text = "🔒";
        this.lblLock.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this.lblLock.Visible = false;
        //
        // btnLock
        //
        this.btnLock.Location = new System.Drawing.Point(170, 214);
        this.btnLock.Name = "btnLock";
        this.btnLock.Size = new System.Drawing.Size(72, 26);
        this.btnLock.Text = "Lock";
        this.btnLock.UseVisualStyleBackColor = true;
        this.btnLock.Click += new System.EventHandler(this.OnLockClick);
        //
        // btnLogoff
        //
        this.btnLogoff.Location = new System.Drawing.Point(248, 214);
        this.btnLogoff.Name = "btnLogoff";
        this.btnLogoff.Size = new System.Drawing.Size(80, 26);
        this.btnLogoff.Text = "Log off";
        this.btnLogoff.UseVisualStyleBackColor = true;
        this.btnLogoff.Click += new System.EventHandler(this.OnLogoffClick);
        //
        // StudentTile
        //
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.Controls.Add(this.lblLock);
        this.Controls.Add(this.lblName);
        this.Controls.Add(this.picThumb);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.btnLock);
        this.Controls.Add(this.btnLogoff);
        this.Margin = new System.Windows.Forms.Padding(6);
        this.Name = "StudentTile";
        this.Size = new System.Drawing.Size(336, 248);
        ((System.ComponentModel.ISupportInitialize)(this.picThumb)).EndInit();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.PictureBox picThumb;
    private System.Windows.Forms.Label lblName;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Label lblLock;
    private System.Windows.Forms.Button btnLock;
    private System.Windows.Forms.Button btnLogoff;
}
