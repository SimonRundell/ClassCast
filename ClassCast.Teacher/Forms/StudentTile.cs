using ClassCast.Teacher.Services;

namespace ClassCast.Teacher.Forms;

/// <summary>
/// A tile in the Teacher Server control panel representing one connected student.
/// Displays the PC name, AD username and a live thumbnail, and offers per-student
/// Lock (toggle) and Log off actions (specification section 4.1).
/// </summary>
public partial class StudentTile : UserControl
{
    private Image? _thumbnail;

    /// <summary>Raised when the Lock/Unlock button is clicked, with the desired lock state.</summary>
    public event Action<StudentSession, bool>? LockRequested;

    /// <summary>Raised when the Log off button is clicked.</summary>
    public event Action<StudentSession>? LogoffRequested;

    /// <summary>Gets the student session this tile represents.</summary>
    public StudentSession Session { get; }

    /// <summary>
    /// Creates a tile bound to a student session.
    /// </summary>
    /// <param name="session">The student session to display.</param>
    public StudentTile(StudentSession session)
    {
        Session = session;
        InitializeComponent();
        RefreshState();
    }

    /// <summary>
    /// Replaces the displayed thumbnail with a freshly decoded image. The previous
    /// image is disposed. Safe to call from any thread.
    /// </summary>
    /// <param name="image">The new thumbnail image; the tile takes ownership.</param>
    public void SetThumbnail(Image image)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetThumbnail(image));
            return;
        }

        Image? old = _thumbnail;
        _thumbnail = image;
        picThumb.Image = image;
        old?.Dispose();
    }

    /// <summary>
    /// Updates the caption, status and lock-button text from the current session
    /// state. Safe to call from any thread.
    /// </summary>
    public void RefreshState()
    {
        if (InvokeRequired)
        {
            BeginInvoke(RefreshState);
            return;
        }

        lblName.Text = Session.DisplayLabel;
        btnLock.Text = Session.IsLocked ? "Unlock" : "Lock";
        lblStatus.Text = Session.IsLocked ? "Locked" : "Connected";
        lblStatus.ForeColor = Session.IsLocked ? Color.Firebrick : Color.ForestGreen;

        // Glanceable lock indicator: padlock badge over the thumbnail plus a red frame.
        lblLock.Visible = Session.IsLocked;
        if (Session.IsLocked)
        {
            lblLock.BringToFront();
        }
        Invalidate(); // repaint the locked/unlocked border
    }

    /// <summary>Draws a bold red frame around the tile while the student is locked.</summary>
    /// <param name="e">The paint event data.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!Session.IsLocked)
        {
            return;
        }

        using var pen = new Pen(Color.Firebrick, 3);
        var frame = new Rectangle(1, 1, Width - 3, Height - 3);
        e.Graphics.DrawRectangle(pen, frame);
    }

    /// <summary>Handles the Lock/Unlock button click.</summary>
    private void OnLockClick(object? sender, EventArgs e)
        => LockRequested?.Invoke(Session, !Session.IsLocked);

    /// <summary>Handles the Log off button click, confirming first.</summary>
    private void OnLogoffClick(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            this,
            $"Log off {Session.MachineName} ({Session.AdUser})?\r\nUnsaved work on the student PC will be lost.",
            "Confirm log off",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            LogoffRequested?.Invoke(Session);
        }
    }
}
