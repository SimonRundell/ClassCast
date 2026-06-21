using ClassCast.Common.Logging;

namespace ClassCast.Student.Forms;

/// <summary>
/// A borderless, top-most, full-screen black overlay that displays the teacher's
/// broadcast. Incoming JPEG frames are decoded and shown centred and scaled
/// (specification section 5.3). The form is created once at startup and shown or
/// hidden as broadcasts start and stop.
/// </summary>
public partial class BroadcastOverlay : Form
{
    private Image? _current;

    /// <summary>Initialises the overlay (created hidden).</summary>
    public BroadcastOverlay()
    {
        InitializeComponent();
        // Force handle creation so the overlay can be used as a UI-thread invoke target
        // even before it is first shown.
        _ = Handle;
    }

    /// <summary>
    /// Shows the overlay full-screen and top-most on the primary monitor. Safe to call
    /// from any thread.
    /// </summary>
    public void ShowOverlay()
    {
        if (InvokeRequired)
        {
            BeginInvoke(ShowOverlay);
            return;
        }

        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Bounds = bounds;
        TopMost = true;
        WindowState = FormWindowState.Normal; // already sized to full bounds
        Show();
        BringToFront();
        Activate();
        Logger.Debug("Broadcast overlay shown.");
    }

    /// <summary>Hides the overlay and releases the current frame. Safe to call from any thread.</summary>
    public void HideOverlay()
    {
        if (InvokeRequired)
        {
            BeginInvoke(HideOverlay);
            return;
        }

        Hide();
        picFrame.Image = null;
        _current?.Dispose();
        _current = null;
        Logger.Debug("Broadcast overlay hidden.");
    }

    /// <summary>
    /// Decodes a JPEG frame and displays it. Safe to call from any thread; decoding is
    /// performed on the calling thread and the resulting image is swapped in on the UI thread.
    /// </summary>
    /// <param name="jpeg">The JPEG frame bytes received from the broadcast channel.</param>
    public void RenderFrame(byte[] jpeg)
    {
        Image image;
        try
        {
            using var ms = new MemoryStream(jpeg);
            using var decoded = Image.FromStream(ms);
            image = new Bitmap(decoded);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to decode broadcast frame: {ex.Message}");
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => SwapImage(image));
        }
        else
        {
            SwapImage(image);
        }
    }

    /// <summary>Swaps the displayed image, disposing the previous one. Runs on the UI thread.</summary>
    private void SwapImage(Image image)
    {
        Image? old = _current;
        _current = image;
        picFrame.Image = image;
        old?.Dispose();
    }

    /// <summary>
    /// Prevents the overlay from being activated/closed by the student via Alt+F4 while
    /// a broadcast is in progress.
    /// </summary>
    /// <param name="e">The closing event arguments.</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Only programmatic disposal (ApplicationExit) may close this form.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }
}
