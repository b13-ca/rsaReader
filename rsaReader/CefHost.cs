using System.Diagnostics;
using CefSharp;
using CefSharp.WinForms;

namespace rsaReader;

internal static class CefHost {
    public static string? StartError { get; private set; }

    public static void Start() {
        if (Cef.IsInitialized == true)
            return;

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "rsaReader",
            "CefSharp");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Cache"));

        var settings = new CefSettings();
        settings.RootCachePath = root;
        settings.CachePath = Path.Combine(root, "Cache");
        settings.Locale = "en-US";
        settings.AcceptLanguageList = "en-US,en";
        settings.BackgroundColor = Cef.ColorSetARGB(255, 255, 255, 255);
        settings.LogSeverity = LogSeverity.Disable;
        settings.BrowserSubprocessPath = ExePath();
        settings.CefCommandLineArgs.Add("in-process-gpu", "1");

        var runtimeDir = RuntimeDirectory();
        if (runtimeDir is not null && !SamePath(runtimeDir, AppContext.BaseDirectory)) {
            settings.ResourcesDirPath = runtimeDir;
            settings.LocalesDirPath = Path.Combine(runtimeDir, "locales");
        }

        try {
            if (!Cef.Initialize(settings, performDependencyCheck: runtimeDir is null || SamePath(runtimeDir, AppContext.BaseDirectory)))
                StartError = "Preview could not start.";
        }
        catch (Exception ex) {
            StartError = ex.Message;
        }
    }

    public static void Stop() {
        if (Cef.IsInitialized == true)
            Cef.Shutdown();
    }

    private static string ExePath() {
        var path = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(path))
            return path;

        return Environment.ProcessPath ?? Application.ExecutablePath;
    }

    private static string? RuntimeDirectory() {
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string search) {
            foreach (var dir in search.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
                var candidate = dir.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (candidate.Length > 0 && File.Exists(Path.Combine(candidate, "libcef.dll")))
                    return candidate;
            }
        }

        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "libcef.dll")))
            return AppContext.BaseDirectory;

        return null;
    }

    private static bool SamePath(string left, string right) {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}
