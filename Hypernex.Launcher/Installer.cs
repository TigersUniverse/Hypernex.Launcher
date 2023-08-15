using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using HypernexSharp;
using HypernexSharp.APIObjects;

namespace Hypernex.Launcher;

public static class Installer
{
    private const string INSTALLING_NAME = "Hypernex.Unity";
    
    private static (string, bool, string[]?)[] UnityDirectories =
    {
        ("Hypernex_Data", false, new[]
        {
            "StreamingAssets"
        }),
        ("MonoBleedingEdge", true, null)
    };

    private static string[] UnityFiles =
    {
        "Hypernex.exe",
        "UnityCrashHandler64.exe",
        "UnityPlayer.dll",
        "xr.bat",
        // Linux
        "Hypernex.x86_64",
        "UnityPlayer.so"
    };

    // 0 Windows, 1 Android, 2 Linux
    private static int ArtifactId
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return 0;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return 2;
            throw new Exception("Unknown Artifact Platform");
        }
    }

    private static void ContinueInstall(Action<bool, string> callback, Action<string, int> progress,
        LauncherCache launcherCache, HypernexObject hypernexObject, string latest, string userid = "", string tokenContent = "")
    {
        progress.Invoke("Getting Build Artifact", 42);
        hypernexObject.GetBuild(buildResult =>
        {
            if (buildResult == Stream.Null)
            {
                callback.Invoke(false, FindExecutable(launcherCache.InstallDirectory));
                return;
            }
            progress.Invoke("Saving Build Artifact", 56);
            MemoryStream ms = new MemoryStream();
            buildResult.CopyTo(ms);
            string savedFile = LauncherCache.SaveFile(ms.ToArray(), "build.zip");
            progress.Invoke("Removing Old Files", 70);
            ClearOld(launcherCache.InstallDirectory);
            progress.Invoke("Extracting Files", 84);
            ZipFile.ExtractToDirectory(savedFile, launcherCache.InstallDirectory, true);
            progress.Invoke("Cleaning Up", 98);
            File.WriteAllText(Path.Combine(launcherCache.InstallDirectory, "version.txt"), latest);
            File.Delete(savedFile);
            ms.Dispose();
            callback.Invoke(true, FindExecutable(launcherCache.InstallDirectory));
        }, INSTALLING_NAME, latest, ArtifactId, new User{Id=userid}, new Token{content = tokenContent});
    }

    // Returns the path of the Executable
    public static void Install(Action<bool, string> callback, Action<string, int> progress, LauncherCache launcherCache)
    {
        progress.Invoke("Initializing", 14);
        HypernexSettings settings = new HypernexSettings { TargetDomain = launcherCache.TargetDomain };
        HypernexObject hypernexObject = new HypernexObject(settings);
        progress.Invoke("Getting Latest Version", 28);
        hypernexObject.GetVersions(versionsResult =>
        {
            if(!versionsResult.success)
            {
                callback.Invoke(false, FindExecutable(launcherCache.InstallDirectory));
                return;
            }
            if (versionsResult.result.Versions.Count <= 0)
            {
                callback.Invoke(false, FindExecutable(launcherCache.InstallDirectory));
                return;
            }
            string latest = versionsResult.result.Versions.First();
            if (IsSameVersion(launcherCache.InstallDirectory, latest))
            {
                callback.Invoke(true, FindExecutable(launcherCache.InstallDirectory));
                return;
            }
            hypernexObject.AuthForBuilds(authResult =>
            {
                if (!authResult.success)
                {
                    callback.Invoke(false, FindExecutable(launcherCache.InstallDirectory));
                    return;
                }
                if (!authResult.result.AuthForBuilds)
                {
                    ContinueInstall(callback, progress, launcherCache, hypernexObject, latest);
                    return;
                }
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    new AuthWindow().SetCallback((didLogin, userid, token, window) =>
                    {
                        window.Close();
                        if (!didLogin)
                        {
                            callback.Invoke(false, FindExecutable(launcherCache.InstallDirectory));
                            return;
                        }
                        ContinueInstall(callback, progress, launcherCache, hypernexObject, latest, userid, token);
                    }, hypernexObject).Show();
                });
            });
        }, INSTALLING_NAME);
    }

    private static bool IsSameVersion(string installLocation, string version)
    {
        string file = Path.Combine(installLocation, "version.txt");
        if (!File.Exists(file))
            return false;
        string t = File.ReadAllText(file);
        return t == version;
    }

    private static void ClearOld(string installDirectory)
    {
        foreach ((string, bool, string[]?) unityDirectory in UnityDirectories)
        {
            string fullPath = Path.Combine(installDirectory, unityDirectory.Item1);
            if (!Directory.Exists(fullPath)) continue;
            if(unityDirectory.Item2)
                Directory.Delete(fullPath, true);
            else
            {
                foreach (string directory in Directory.GetDirectories(fullPath))
                {
                    if(!unityDirectory.Item3!.Contains(new DirectoryInfo(directory).Name))
                        Directory.Delete(directory, true);
                }
                foreach (string file in Directory.GetFiles(fullPath))
                {
                    if(!unityDirectory.Item3!.Contains(Path.GetFileName(file)))
                        File.Delete(file);
                }
            }
        }
        foreach (string unityFile in UnityFiles)
        {
            if(File.Exists(unityFile))
                File.Delete(unityFile);
        }
    }

    private static string FindExecutable(string installLocation)
    {
        foreach (string file in Directory.GetFiles(installLocation))
        {
            if (Path.GetFileNameWithoutExtension(file).ToLower() == "hypernex")
                return file;
        }
        throw new Exception("No executable!");
    }
}