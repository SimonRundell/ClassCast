using System.Diagnostics;
using ClassCast.Common.Logging;

namespace ClassCast.Common.Media;

/// <summary>
/// Wraps a child <c>ffmpeg.exe</c> process configured as a streaming encoder:
/// raw BGR24 frames are written to its standard input, and it emits a continuous
/// MJPEG stream on standard output. This wrapper parses that stream into discrete
/// JPEG frames (delimited by the SOI <c>0xFF 0xD8</c> and EOI <c>0xFF 0xD9</c>
/// markers) and raises <see cref="FrameReady"/> for each complete frame.
/// </summary>
/// <remarks>
/// Implements the encoding approach described in specification section 4.3. One
/// instance encodes a single stream (e.g. the broadcast). Call <see cref="Start"/>,
/// feed frames with <see cref="WriteFrameAsync"/>, and dispose when finished.
/// </remarks>
public sealed class FFmpegWrapper : IDisposable
{
    private const byte Marker = 0xFF;
    private const byte Soi = 0xD8; // Start Of Image
    private const byte Eoi = 0xD9; // End Of Image

    private readonly string _ffmpegPath;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly int _outputWidth;
    private readonly int _outputHeight;
    private readonly int _fps;
    private readonly int _quality;

    private Process? _process;
    private Stream? _stdin;
    private Task? _readTask;
    private Task? _stderrTask;
    private CancellationTokenSource? _cts;
    private readonly object _writeGate = new();

    /// <summary>
    /// Raised once for every complete JPEG frame decoded from the ffmpeg output
    /// stream. The byte array begins with <c>0xFF 0xD8</c> and ends with <c>0xFF 0xD9</c>.
    /// </summary>
    public event Action<byte[]>? FrameReady;

    /// <summary>Gets a value indicating whether the encoder process is running.</summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Initialises a new encoder.
    /// </summary>
    /// <param name="ffmpegPath">Absolute path to <c>ffmpeg.exe</c>.</param>
    /// <param name="inputWidth">Width of the raw BGR24 frames fed to <see cref="WriteFrameAsync"/>.</param>
    /// <param name="inputHeight">Height of the raw BGR24 frames.</param>
    /// <param name="outputWidth">Width that frames are scaled to before encoding.</param>
    /// <param name="outputHeight">Height that frames are scaled to before encoding.</param>
    /// <param name="fps">Declared input frame rate.</param>
    /// <param name="quality">MJPEG quality factor (<c>-q:v</c>); lower is higher quality.</param>
    public FFmpegWrapper(string ffmpegPath, int inputWidth, int inputHeight,
                         int outputWidth, int outputHeight, int fps, int quality)
    {
        _ffmpegPath = ffmpegPath;
        _inputWidth = inputWidth;
        _inputHeight = inputHeight;
        _outputWidth = outputWidth;
        _outputHeight = outputHeight;
        _fps = fps;
        _quality = quality;
    }

    /// <summary>Builds the ffmpeg command-line arguments for streaming MJPEG encoding.</summary>
    /// <returns>The argument string passed to <c>ffmpeg.exe</c>.</returns>
    private string BuildArguments() =>
        $"-hide_banner -loglevel error " +
        $"-f rawvideo -pixel_format bgr24 -video_size {_inputWidth}x{_inputHeight} " +
        $"-framerate {_fps} -i pipe:0 " +
        $"-vf scale={_outputWidth}:{_outputHeight} -c:v mjpeg -q:v {_quality} " +
        $"-f mjpeg -flush_packets 1 pipe:1";

    /// <summary>
    /// Launches the ffmpeg child process and starts the background reader that
    /// decodes JPEG frames from its standard output.
    /// </summary>
    /// <exception cref="FileNotFoundException">The ffmpeg executable was not found.</exception>
    /// <exception cref="InvalidOperationException">The encoder is already running.</exception>
    public void Start()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("FFmpegWrapper is already running.");
        }

        if (!File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                $"ffmpeg.exe not found at '{_ffmpegPath}'. See README.md for download instructions.",
                _ffmpegPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = BuildArguments(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
        _stdin = _process.StandardInput.BaseStream;
        _cts = new CancellationTokenSource();

        Logger.Info($"FFmpeg encoder started ({_inputWidth}x{_inputHeight} -> {_outputWidth}x{_outputHeight} @ {_fps}fps, q={_quality}).");

        _readTask = Task.Run(() => ReadLoopAsync(_process.StandardOutput.BaseStream, _cts.Token));
        _stderrTask = Task.Run(() => DrainStderrAsync(_process.StandardError, _cts.Token));
    }

    /// <summary>
    /// Writes a single raw BGR24 frame to the encoder's standard input. Frames must
    /// match the input width/height supplied to the constructor.
    /// </summary>
    /// <param name="bgr24">Tightly packed BGR24 pixel data (<c>width * height * 3</c> bytes).</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the frame has been written.</returns>
    public async Task WriteFrameAsync(ReadOnlyMemory<byte> bgr24, CancellationToken cancellationToken = default)
    {
        Stream? stdin = _stdin;
        if (stdin is null || !IsRunning)
        {
            return;
        }

        try
        {
            await stdin.WriteAsync(bgr24, cancellationToken).ConfigureAwait(false);
            await stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Logger.Warn($"FFmpeg stdin write failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Background loop that reads the MJPEG stdout stream and re-assembles complete
    /// JPEG frames using a SOI/EOI marker state machine.
    /// </summary>
    private async Task ReadLoopAsync(Stream stdout, CancellationToken token)
    {
        byte[] buffer = new byte[64 * 1024];
        using var frame = new MemoryStream();
        bool inFrame = false;
        byte last = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                int read = await stdout.ReadAsync(buffer, token).ConfigureAwait(false);
                if (read == 0)
                {
                    break; // ffmpeg closed stdout
                }

                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];

                    if (inFrame)
                    {
                        frame.WriteByte(b);
                        if (last == Marker && b == Eoi)
                        {
                            // Complete JPEG assembled.
                            FrameReady?.Invoke(frame.ToArray());
                            frame.SetLength(0);
                            inFrame = false;
                            last = 0;
                            continue;
                        }
                    }
                    else if (last == Marker && b == Soi)
                    {
                        // Start of a new JPEG; emit the SOI marker bytes.
                        frame.WriteByte(Marker);
                        frame.WriteByte(Soi);
                        inFrame = true;
                        last = 0;
                        continue;
                    }

                    last = b;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            Logger.Error("FFmpeg read loop failed.", ex);
        }
    }

    /// <summary>Continuously drains ffmpeg's stderr so the pipe never blocks; logs any output.</summary>
    private async Task DrainStderrAsync(StreamReader stderr, CancellationToken token)
    {
        try
        {
            string? line;
            while (!token.IsCancellationRequested && (line = await stderr.ReadLineAsync(token).ConfigureAwait(false)) is not null)
            {
                if (line.Length > 0)
                {
                    Logger.Debug($"[ffmpeg] {line}");
                }
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception ex)
        {
            Logger.Debug($"FFmpeg stderr drain ended: {ex.Message}");
        }
    }

    /// <summary>Stops the encoder, closing stdin and terminating the child process.</summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();

            lock (_writeGate)
            {
                try { _stdin?.Close(); } catch { /* ignore */ }
            }

            if (_process is { HasExited: false } p)
            {
                if (!p.WaitForExit(2000))
                {
                    p.Kill(entireProcessTree: true);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Error stopping ffmpeg: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            _stdin = null;
        }
    }

    /// <summary>Releases the encoder process and all associated resources.</summary>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _cts = null;
    }
}
