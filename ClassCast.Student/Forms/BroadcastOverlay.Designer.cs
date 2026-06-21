#nullable disable
namespace ClassCast.Student.Forms;

partial class BroadcastOverlay
{
    private System.ComponentModel.IContainer components = null;

    /// <summary>Clean up any resources being used.</summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _current?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.picFrame = new System.Windows.Forms.PictureBox();
        ((System.ComponentModel.ISupportInitialize)(this.picFrame)).BeginInit();
        this.SuspendLayout();
        //
        // picFrame
        //
        this.picFrame.BackColor = System.Drawing.Color.Black;
        this.picFrame.Dock = System.Windows.Forms.DockStyle.Fill;
        this.picFrame.Name = "picFrame";
        this.picFrame.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.picFrame.TabStop = false;
        //
        // BroadcastOverlay
        //
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.BackColor = System.Drawing.Color.Black;
        this.ClientSize = new System.Drawing.Size(854, 480);
        this.Controls.Add(this.picFrame);
        this.ControlBox = false;
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        this.Name = "BroadcastOverlay";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        this.Text = "ClassCast Broadcast";
        ((System.ComponentModel.ISupportInitialize)(this.picFrame)).EndInit();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.PictureBox picFrame;
}
