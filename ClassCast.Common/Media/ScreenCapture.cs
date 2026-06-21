using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClassCast.Common.Media;

/// <summary>
/// Captures the primary screen using GDI+ <c>Graphics.CopyFromScreen</c> (BitBlt).
/// Chosen as the primary capture method for broad .NET 8 WinForms compatibility,
/// as recommended by the ClassCast specification (section 4.2).
/// </summary>
public static class ScreenCapture
{
    /// <summary>
    /// Captures the screen region described by <paramref name="bounds"/> (virtual-desktop
    /// coordinates, as reported by <see cref="Screen.Bounds"/>) into a new 32-bit ARGB bitmap.
    /// The caller owns the returned bitmap and must dispose it.
    /// </summary>
    public static Bitmap CaptureScreen(Rectangle bounds)
    {
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    /// <summary>Captures the full primary screen into a new 32-bit ARGB bitmap.</summary>
    public static Bitmap CapturePrimaryScreen()
        => CaptureScreen(Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080));

    /// <summary>Captures a specific screen region as tightly-packed 24-bit BGR bytes for FFmpeg.</summary>
    public static byte[] CaptureScreenBgr24(Rectangle bounds, out int width, out int height)
    {
        using Bitmap bmp = CaptureScreen(bounds);
        width = bmp.Width;
        height = bmp.Height;
        return ToBgr24(bmp);
    }

    /// <summary>Captures the primary screen as tightly-packed 24-bit BGR bytes.</summary>
    public static byte[] CapturePrimaryScreenBgr24(out int width, out int height)
        => CaptureScreenBgr24(Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080), out width, out height);

    /// <summary>
    /// Converts a bitmap to tightly packed BGR24 bytes (no row padding), which is
    /// the layout FFmpeg expects for <c>rawvideo / bgr24</c> input.
    /// </summary>
    /// <param name="source">The source bitmap.</param>
    /// <returns>Row-major BGR24 pixel data with <c>width * 3</c> bytes per row.</returns>
    public static byte[] ToBgr24(Bitmap source)
    {
        int width = source.Width;
        int height = source.Height;
        var rect = new Rectangle(0, 0, width, height);

        // Lock the bitmap as 24bppRgb; GDI+ stores this channel order as B, G, R.
        BitmapData data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int rowBytes = width * 3;
            byte[] result = new byte[rowBytes * height];
            nint scan0 = data.Scan0;
            for (int y = 0; y < height; y++)
            {
                // Copy each row, stripping the GDI+ stride padding.
                System.Runtime.InteropServices.Marshal.Copy(
                    scan0 + (y * data.Stride),
                    result,
                    y * rowBytes,
                    rowBytes);
            }
            return result;
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    /// <summary>
    /// Encodes a bitmap as a JPEG byte array at the given quality, using GDI+.
    /// Used by the thumbnail pipeline as a lightweight alternative to FFmpeg.
    /// </summary>
    /// <param name="source">The bitmap to encode.</param>
    /// <param name="quality">JPEG quality, 0&#8211;100 (higher is better).</param>
    /// <returns>The encoded JPEG bytes.</returns>
    public static byte[] EncodeJpeg(Bitmap source, int quality)
    {
        ImageCodecInfo? jpegCodec = Array.Find(
            ImageCodecInfo.GetImageEncoders(),
            c => c.FormatID == ImageFormat.Jpeg.Guid);

        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(quality, 0, 100));

        using var ms = new MemoryStream();
        if (jpegCodec is not null)
        {
            source.Save(ms, jpegCodec, encoderParams);
        }
        else
        {
            source.Save(ms, ImageFormat.Jpeg);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Captures the primary screen, scales it to the requested size, and returns a
    /// JPEG byte array. Used by the Student thumbnail pipeline.
    /// </summary>
    /// <param name="width">Target thumbnail width.</param>
    /// <param name="height">Target thumbnail height.</param>
    /// <param name="quality">JPEG quality, 0&#8211;100.</param>
    /// <returns>The scaled JPEG bytes.</returns>
    public static byte[] CaptureScaledJpeg(int width, int height, int quality)
    {
        using Bitmap full = CapturePrimaryScreen();
        using var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(full, 0, 0, width, height);
        }
        return EncodeJpeg(scaled, quality);
    }
}
