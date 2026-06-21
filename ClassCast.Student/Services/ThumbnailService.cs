using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Media;

namespace ClassCast.Student.Services;

/// <summary>
/// Periodically captures the student's primary screen, scales and JPEG-encodes it,
/// and forwards it (base64-encoded) over the control channel as a THUMBNAIL message
/// (specification section 6). Capture is automatically suspended while a broadcast is
/// being displayed, to avoid unnecessary traffic.
/// </summary>
public sealed class ThumbnailService : IDisposable
{
    private const int JpegQuality = 40;

    private readonly AppConfig _config;
    private readonly Func<string, Task<bool>> _sendThumbnail;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _suspended;

    /// <summary>
    /// Initialises the thumbnail service.
    /// </summary>
    /// <param name="config">Configuration providing thumbnail size and rate.</param>
    /// <param name="sendThumbnail">
    /// Callback that transmits a base64-encoded JPEG and reports success.
    /// </param>
    public ThumbnailService(AppConfig config, Func<string, Task<bool>> sendThumbnail)
    {
        _config = config;
        _sendThumbnail = sendThumbnail;
    }

    /// <summary>Starts the capture loop on a background task.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => CaptureLoopAsync(_cts.Token));
    }

    /// <summary>Suspends or resumes thumbnail capture (used during broadcast display).</summary>
    /// <param name="suspended"><c>true</c> to pause capture; <c>false</c> to resume.</param>
    public void SetSuspended(bool suspended)
    {
        _suspended = suspended;
        Logger.Debug($"Thumbnail capture {(suspended ? "suspended" : "resumed")}.");
    }

    /// <summary>Captures and sends a thumbnail at the configured frame rate.</summary>
    private async Task CaptureLoopAsync(CancellationToken token)
    {
        int intervalMs = Math.Max(200, 1000 / Math.Max(1, _config.ThumbnailFps));

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_suspended)
                {
                    // Screen capture must run on a thread without unexpected reentrancy;
                    // it is CPU work, so offload it explicitly.
                    string base64 = await Task.Run(() =>
                    {
                        byte[] jpeg = ScreenCapture.CaptureScaledJpeg(
                            _config.ThumbnailWidth, _config.ThumbnailHeight, JpegQuality);
                        return Convert.ToBase64String(jpeg);
                    }, token).ConfigureAwait(false);

                    await _sendThumbnail(base64).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Thumbnail capture error: {ex.Message}");
            }

            try { await Task.Delay(intervalMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Stops the capture loop and releases resources.</summary>
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        _cts?.Dispose();
    }
}
