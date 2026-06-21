using ClassCast.Common.Config;

namespace ClassCast.Tests;

/// <summary>
/// Verifies that <see cref="AppConfig"/> parses a sample <c>config.json</c> correctly
/// and falls back to defaults when the file is missing (specification section 11).
/// </summary>
public class AppConfigTests
{
    [Fact]
    public void Load_ParsesAllTeacherValues()
    {
        string json = """
        {
          "adDomain":          "ACME",
          "adTeacherGroup":    "Staff-Teachers",
          "udpDiscoveryPort":  45678,
          "tcpControlPort":    45679,
          "tcpBroadcastPort":  45680,
          "broadcastWidth":    854,
          "broadcastHeight":   480,
          "broadcastFps":      15,
          "thumbnailFps":      1,
          "thumbnailWidth":    320,
          "thumbnailHeight":   180,
          "ffmpegPath":        "ffmpeg\\ffmpeg.exe"
        }
        """;

        string path = Path.Combine(Path.GetTempPath(), $"classcast-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try
        {
            AppConfig config = AppConfig.Load(path);

            Assert.Equal("ACME", config.AdDomain);
            Assert.Equal("Staff-Teachers", config.AdTeacherGroup);
            Assert.Equal(45678, config.UdpDiscoveryPort);
            Assert.Equal(45679, config.TcpControlPort);
            Assert.Equal(45680, config.TcpBroadcastPort);
            Assert.Equal(854, config.BroadcastWidth);
            Assert.Equal(480, config.BroadcastHeight);
            Assert.Equal(15, config.BroadcastFps);
            Assert.Equal(1, config.ThumbnailFps);
            Assert.Equal(320, config.ThumbnailWidth);
            Assert.Equal(180, config.ThumbnailHeight);
            Assert.Equal("ffmpeg\\ffmpeg.exe", config.FfmpegPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        AppConfig config = AppConfig.Load(missing);

        Assert.Equal(45678, config.UdpDiscoveryPort);
        Assert.Equal(45679, config.TcpControlPort);
        Assert.Equal(45680, config.TcpBroadcastPort);
        Assert.Equal("ClassCast-Teachers", config.AdTeacherGroup);
    }

    [Fact]
    public void Load_PartialStudentConfig_KeepsDefaultsForUnspecified()
    {
        string json = """
        {
          "udpDiscoveryPort":  45678,
          "tcpControlPort":    45679,
          "tcpBroadcastPort":  45680
        }
        """;

        string path = Path.Combine(Path.GetTempPath(), $"classcast-student-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try
        {
            AppConfig config = AppConfig.Load(path);

            Assert.Equal(45678, config.UdpDiscoveryPort);
            // Unspecified teacher-only fields retain their documented defaults.
            Assert.Equal(854, config.BroadcastWidth);
            Assert.Equal(15, config.BroadcastFps);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DefaultsToActiveDirectoryAuth()
    {
        // A config without an authMode key must behave exactly as before.
        var config = new AppConfig();
        Assert.Equal("ActiveDirectory", config.AuthMode);
        Assert.False(config.UseWorkgroupAuth);
    }

    [Theory]
    [InlineData("Workgroup")]
    [InlineData("workgroup")]
    [InlineData("Local")]
    public void Load_WorkgroupAuthMode_IsRecognised(string mode)
    {
        string json = $$"""
        {
          "authMode": "{{mode}}",
          "teacherPasswordHash": "pbkdf2$100000$c2FsdA==$aGFzaA=="
        }
        """;

        string path = Path.Combine(Path.GetTempPath(), $"classcast-wg-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try
        {
            AppConfig config = AppConfig.Load(path);
            Assert.True(config.UseWorkgroupAuth);
            Assert.Equal("pbkdf2$100000$c2FsdA==$aGFzaA==", config.TeacherPasswordHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_RoundTripsThroughLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"classcast-save-{Guid.NewGuid():N}.json");
        try
        {
            var original = new AppConfig { AuthMode = "Workgroup", TeacherPasswordHash = "abc" };
            original.Save(path);

            AppConfig reloaded = AppConfig.Load(path);
            Assert.Equal("Workgroup", reloaded.AuthMode);
            Assert.Equal("abc", reloaded.TeacherPasswordHash);
            Assert.True(reloaded.UseWorkgroupAuth);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolveFfmpegPath_MakesRelativePathAbsolute()
    {
        var config = new AppConfig { FfmpegPath = "ffmpeg\\ffmpeg.exe" };
        string resolved = config.ResolveFfmpegPath();

        Assert.True(Path.IsPathRooted(resolved));
        Assert.EndsWith(Path.Combine("ffmpeg", "ffmpeg.exe"), resolved);
    }
}
