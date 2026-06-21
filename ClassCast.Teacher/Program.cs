using System.Runtime.Versioning;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Security;
using ClassCast.Teacher.Forms;

namespace ClassCast.Teacher;

/// <summary>
/// Entry point for the ClassCast Teacher Server application.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Program
{
    /// <summary>The Teacher Server version reported to clients.</summary>
    public const string Version = "1.0.0";

    /// <summary>Command-line switch used by the installer to provision the workgroup password.</summary>
    private const string SetPasswordSwitch = "--set-teacher-password";

    /// <summary>
    /// Application entry point. Supports a headless installer hook to set the workgroup
    /// password, otherwise loads configuration, shows the appropriate login dialog and,
    /// on success, launches the main control panel.
    /// </summary>
    /// <param name="args">
    /// When the first argument is <c>--set-teacher-password</c>, the second argument is
    /// hashed and written to <c>config.json</c> and the process exits. This lets the
    /// (elevated) installer store the shared password where the running app, which has
    /// no write access to Program Files, only needs to read it.
    /// </param>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0].Equals(SetPasswordSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return SetTeacherPassword(args.Length >= 2 ? args[1] : null);
        }

        ApplicationConfiguration.Initialize();
        Logger.Configure("Teacher");
        Logger.Info($"ClassCast Teacher {Version} starting.");

        AppConfig config = AppConfig.Load();

        Form login = config.UseWorkgroupAuth
            ? new WorkgroupLoginForm(config)
            : new LoginForm(config);

        using (login)
        {
            DialogResult result = login.ShowDialog();
            if (result != DialogResult.OK)
            {
                Logger.Info("Login cancelled or failed; exiting.");
                return 0;
            }

            string teacher = login is WorkgroupLoginForm wg
                ? wg.AuthenticatedTeacher
                : ((LoginForm)login).AuthenticatedTeacher;

            Application.Run(new MainForm(config, teacher, Version));
        }

        Logger.Info("ClassCast Teacher exited.");
        return 0;
    }

    /// <summary>
    /// Hashes the supplied password and writes it to <c>config.json</c>. Invoked by the
    /// installer with elevated rights so the hash can be stored beside the executable.
    /// </summary>
    /// <param name="password">The plain-text password, or <c>null</c> if none was supplied.</param>
    /// <returns>0 on success; 1 on failure.</returns>
    private static int SetTeacherPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 1;
        }

        try
        {
            AppConfig config = AppConfig.Load();
            config.TeacherPasswordHash = PasswordHasher.Hash(password);
            config.Save();
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}
