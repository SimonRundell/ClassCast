using System.Text.Json;
using System.Text.Json.Serialization;
using ClassCast.Common.Logging;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Stores per-teacher, per-machine UI preferences that must survive a restart but
/// cannot be written back to <c>config.json</c> (which lives in Program Files and is
/// read-only for a standard user). The file is kept in the user's roaming
/// <c>%APPDATA%\ClassCast</c> folder, which is always writable.
/// </summary>
public sealed class TeacherPreferences
{
    /// <summary>The chosen broadcast quality preset key, or <c>null</c> if never set.</summary>
    [JsonPropertyName("broadcastQuality")]
    public string? BroadcastQuality { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Full path to the per-user preferences file.</summary>
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClassCast",
        "teacher-prefs.json");

    /// <summary>
    /// Loads the saved preferences, returning an empty instance if none exist or the
    /// file cannot be read.
    /// </summary>
    /// <returns>The loaded preferences (never <c>null</c>).</returns>
    public static TeacherPreferences Load()
    {
        try
        {
            string path = FilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<TeacherPreferences>(json, Options) ?? new TeacherPreferences();
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Could not read teacher preferences: {ex.Message}");
        }
        return new TeacherPreferences();
    }

    /// <summary>Saves the current preferences, creating the folder if necessary.</summary>
    public void Save()
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not save teacher preferences: {ex.Message}");
        }
    }
}
