using ClassCast.Common.Media;

namespace ClassCast.Tests;

/// <summary>
/// Verifies that <see cref="FFmpegWrapper"/> produces a valid JPEG (begins with the
/// SOI marker <c>0xFF 0xD8</c>) from a raw test bitmap (specification section 11).
/// </summary>
/// <remarks>
/// The test locates the ffmpeg binary bundled under
/// <c>ClassCast.Teacher/ffmpeg/ffmpeg.exe</c>. If it cannot be found (e.g. before the
/// binary has been downloaded) the test is skipped rather than failed.
/// </remarks>
public class FFmpegWrapperTests
{
    private const int Width = 64;
    private const int Height = 48;

    [Fact]
    public async Task Encode_ProducesJpegWithSoiMarker()
    {
        string? ffmpeg = LocateFfmpeg();
        if (ffmpeg is null)
        {
            // ffmpeg binary not present (see README for download instructions); nothing to test.
            return;
        }

        using var wrapper = new FFmpegWrapper(ffmpeg, Width, Height, Width, Height, fps: 5, quality: 4);

        var firstFrame = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        wrapper.FrameReady += frame => firstFrame.TrySetResult(frame);
        wrapper.Start();

        // Feed identical grey frames continuously until the encoder emits a frame
        // (or we time out). Streaming encoders need a steady input to produce output.
        byte[] raw = new byte[Width * Height * 3];
        Array.Fill(raw, (byte)128);

        using var feedCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Task feeder = Task.Run(async () =>
        {
            while (!feedCts.IsCancellationRequested && !firstFrame.Task.IsCompleted)
            {
                await wrapper.WriteFrameAsync(raw, feedCts.Token);
                await Task.Delay(50, feedCts.Token);
            }
        }, feedCts.Token);

        Task completed = await Task.WhenAny(firstFrame.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        feedCts.Cancel();
        try { await feeder; } catch (OperationCanceledException) { /* expected */ }

        Assert.True(completed == firstFrame.Task, "Timed out waiting for an encoded frame from ffmpeg.");

        byte[] jpeg = await firstFrame.Task;
        Assert.True(jpeg.Length >= 2, "Encoded frame is too short.");
        Assert.Equal(0xFF, jpeg[0]); // SOI marker, byte 1
        Assert.Equal(0xD8, jpeg[1]); // SOI marker, byte 2
        Assert.Equal(0xFF, jpeg[^2]); // EOI marker, byte 1
        Assert.Equal(0xD9, jpeg[^1]); // EOI marker, byte 2
    }

    /// <summary>
    /// Walks up from the test output directory to find the solution root, then returns
    /// the path to the bundled ffmpeg binary, or <c>null</c> if it is not present.
    /// </summary>
    private static string? LocateFfmpeg()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ClassCast.sln")))
            {
                string candidate = Path.Combine(dir.FullName, "ClassCast.Teacher", "ffmpeg", "ffmpeg.exe");
                return File.Exists(candidate) ? candidate : null;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
