#nullable disable
namespace ClassCast.Teacher.Forms;

partial class MainForm
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
        this.toolBar = new System.Windows.Forms.ToolStrip();
        this.tsbBroadcast = new System.Windows.Forms.ToolStripSplitButton();
        this.tsbQuality = new System.Windows.Forms.ToolStripDropDownButton();
        this.tsSepQ = new System.Windows.Forms.ToolStripSeparator();
        this.tsSep1 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbLockAll = new System.Windows.Forms.ToolStripButton();
        this.tsbUnlockAll = new System.Windows.Forms.ToolStripButton();
        this.tsSep2 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbLogoffAll = new System.Windows.Forms.ToolStripButton();
        this.flowStudents = new System.Windows.Forms.FlowLayoutPanel();
        this.statusBar = new System.Windows.Forms.StatusStrip();
        this.lblConnected = new System.Windows.Forms.ToolStripStatusLabel();
        this.lblSep1 = new System.Windows.Forms.ToolStripStatusLabel();
        this.lblBroadcast = new System.Windows.Forms.ToolStripStatusLabel();
        this.lblSpring = new System.Windows.Forms.ToolStripStatusLabel();
        this.lblTeacher = new System.Windows.Forms.ToolStripStatusLabel();
        this.toolBar.SuspendLayout();
        this.statusBar.SuspendLayout();
        this.SuspendLayout();
        //
        // toolBar
        //
        this.toolBar.ImageScalingSize = new System.Drawing.Size(20, 20);
        this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbBroadcast, this.tsbQuality, this.tsSepQ, this.tsSep1, this.tsbLockAll, this.tsbUnlockAll, this.tsSep2, this.tsbLogoffAll });
        this.toolBar.Location = new System.Drawing.Point(0, 0);
        this.toolBar.Name = "toolBar";
        this.toolBar.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
        this.toolBar.Size = new System.Drawing.Size(1064, 31);
        //
        // tsbBroadcast
        //
        this.tsbBroadcast.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsbBroadcast.Name = "tsbBroadcast";
        this.tsbBroadcast.Text = "Start Broadcast";
        this.tsbBroadcast.ButtonClick += new System.EventHandler(this.OnBroadcastButtonClick);
        this.tsbBroadcast.DropDownOpening += new System.EventHandler(this.OnBroadcastDropDownOpening);
        //
        // tsbQuality
        //
        this.tsbQuality.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsbQuality.Name = "tsbQuality";
        this.tsbQuality.Text = "Quality";
        //
        // tsbLockAll
        //
        this.tsbLockAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsbLockAll.Name = "tsbLockAll";
        this.tsbLockAll.Text = "Lock All";
        this.tsbLockAll.Click += new System.EventHandler(this.OnLockAllClick);
        //
        // tsbUnlockAll
        //
        this.tsbUnlockAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsbUnlockAll.Name = "tsbUnlockAll";
        this.tsbUnlockAll.Text = "Unlock All";
        this.tsbUnlockAll.Click += new System.EventHandler(this.OnUnlockAllClick);
        //
        // tsbLogoffAll
        //
        this.tsbLogoffAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        this.tsbLogoffAll.Name = "tsbLogoffAll";
        this.tsbLogoffAll.Text = "Log off All";
        this.tsbLogoffAll.Click += new System.EventHandler(this.OnLogoffAllClick);
        //
        // flowStudents
        //
        this.flowStudents.AutoScroll = true;
        this.flowStudents.BackColor = System.Drawing.Color.WhiteSmoke;
        this.flowStudents.Dock = System.Windows.Forms.DockStyle.Fill;
        this.flowStudents.Location = new System.Drawing.Point(0, 31);
        this.flowStudents.Name = "flowStudents";
        this.flowStudents.Padding = new System.Windows.Forms.Padding(8);
        this.flowStudents.Size = new System.Drawing.Size(1064, 600);
        //
        // statusBar
        //
        this.statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblConnected, this.lblSep1, this.lblBroadcast, this.lblSpring, this.lblTeacher });
        this.statusBar.Location = new System.Drawing.Point(0, 631);
        this.statusBar.Name = "statusBar";
        this.statusBar.Size = new System.Drawing.Size(1064, 22);
        //
        // lblConnected
        //
        this.lblConnected.Name = "lblConnected";
        this.lblConnected.Text = "Students: 0";
        //
        // lblSep1
        //
        this.lblSep1.Name = "lblSep1";
        this.lblSep1.Text = "|";
        //
        // lblBroadcast
        //
        this.lblBroadcast.Name = "lblBroadcast";
        this.lblBroadcast.Text = "Broadcast: off";
        //
        // lblSpring
        //
        this.lblSpring.Name = "lblSpring";
        this.lblSpring.Spring = true;
        this.lblSpring.Text = "";
        //
        // lblTeacher
        //
        this.lblTeacher.Name = "lblTeacher";
        this.lblTeacher.Text = "Not signed in";
        //
        // MainForm
        //
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.ClientSize = new System.Drawing.Size(1064, 653);
        this.Controls.Add(this.flowStudents);
        this.Controls.Add(this.statusBar);
        this.Controls.Add(this.toolBar);
        this.MinimumSize = new System.Drawing.Size(640, 400);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "ClassCast Teacher";
        this.toolBar.ResumeLayout(false);
        this.toolBar.PerformLayout();
        this.statusBar.ResumeLayout(false);
        this.statusBar.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.ToolStrip toolBar;
    private System.Windows.Forms.ToolStripSplitButton tsbBroadcast;
    private System.Windows.Forms.ToolStripDropDownButton tsbQuality;
    private System.Windows.Forms.ToolStripSeparator tsSepQ;
    private System.Windows.Forms.ToolStripSeparator tsSep1;
    private System.Windows.Forms.ToolStripButton tsbLockAll;
    private System.Windows.Forms.ToolStripButton tsbUnlockAll;
    private System.Windows.Forms.ToolStripSeparator tsSep2;
    private System.Windows.Forms.ToolStripButton tsbLogoffAll;
    private System.Windows.Forms.FlowLayoutPanel flowStudents;
    private System.Windows.Forms.StatusStrip statusBar;
    private System.Windows.Forms.ToolStripStatusLabel lblConnected;
    private System.Windows.Forms.ToolStripStatusLabel lblSep1;
    private System.Windows.Forms.ToolStripStatusLabel lblBroadcast;
    private System.Windows.Forms.ToolStripStatusLabel lblSpring;
    private System.Windows.Forms.ToolStripStatusLabel lblTeacher;
}
