using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using HypernexSharp;
using HypernexSharp.APIObjects;

namespace Hypernex.Launcher;

public static class Installer
{
    private const string INSTALLING_NAME = "Hypernex.Unity";
    public const string GIT_URL = "https://github.com/TigersUniverse/" + INSTALLING_NAME + "/releases/latest";
    
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
    
    private static string GitHubDownload
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return
                    "https://github.com/TigersUniverse/Hypernex.Unity/releases/latest/download/Hypernex_win-x64.zip";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return
                    "https://github.com/TigersUniverse/Hypernex.Unity/releases/latest/download/Hypernex_linux-x64.zip";
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
    
    private static void ContinueInstall(Action<bool, string> callback, Action<string, int> progress,
        LauncherCache launcherCache, string buildResult, string latest)
    {
        progress.Invoke("Removing Old Files", 60);
        ClearOld(launcherCache.InstallDirectory);
        progress.Invoke("Extracting Files", 80);
        ZipFile.ExtractToDirectory(buildResult, launcherCache.InstallDirectory, true);
        progress.Invoke("Cleaning Up", 100);
        File.WriteAllText(Path.Combine(launcherCache.InstallDirectory, "version.txt"), latest);
        File.Delete(buildResult);
        callback.Invoke(true, FindExecutable(launcherCache.InstallDirectory));
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

    public static void InstallGithub(Action<bool, string> callback, Action<string, int> progress, LauncherCache launcherCache)
    {
        progress.Invoke("Getting Latest Version", 20);
        string version = GetLatestVersionFromGitHub();
        if (IsSameVersion(launcherCache.InstallDirectory, version))
        {
            callback.Invoke(true, FindExecutable(launcherCache.InstallDirectory));
            return;
        }
        progress.Invoke("Getting Build Artifact", 40);
        using WebClient client = new WebClient();
        string outputFile = LauncherCache.GetFileSave("build.zip");
        client.DownloadFileCompleted += (sender, args) =>
            ContinueInstall(callback, progress, launcherCache, outputFile, version);
        client.DownloadProgressChanged += (sender, args) =>
            progress.Invoke("Downloading latest version... (" + args.ProgressPercentage + "%)",
                args.ProgressPercentage);
        client.DownloadFileAsync(new Uri(GitHubDownload), outputFile);
    }

    private static bool IsSameVersion(string installLocation, string version)
    {
        string file = Path.Combine(installLocation, "version.txt");
        if (!File.Exists(file))
            return false;
        string t = File.ReadAllText(file);
        return t == version;
    }
    
    public static string GetLatestVersionFromGitHub()
    {
        // Get the URL
        string url = GetFinalRedirect(GIT_URL);
        if (!string.IsNullOrEmpty(url))
        {
            // Parse the Url
            string[] slashSplit = url.Split('/');
            string tag = slashSplit[slashSplit.Length - 1];
            return tag;
        }
        return String.Empty;
    }
    
    /// <summary>
    /// Method by Marcelo Calbucci and edited by Uwe Keim. 
    /// No changes to this method were made. 
    /// https://stackoverflow.com/a/28424940/12968919
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private static string GetFinalRedirect(string url)
    {
        if(string.IsNullOrWhiteSpace(url))
            return url;
        
        int maxRedirCount = 8;  // prevent infinite loops
        string newUrl = url;
        do
        {
            HttpWebRequest req = null;
            HttpWebResponse resp = null;
            try
            {
                req = (HttpWebRequest) HttpWebRequest.Create(url);
                req.Method = "HEAD";
                req.AllowAutoRedirect = false;
                resp = (HttpWebResponse)req.GetResponse();
                switch (resp.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return newUrl;
                    case HttpStatusCode.Redirect:
                    case HttpStatusCode.MovedPermanently:
                    case HttpStatusCode.RedirectKeepVerb:
                    case HttpStatusCode.RedirectMethod:
                        newUrl = resp.Headers["Location"];
                        if (newUrl == null)
                            return url;
        
                        if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                        {
                            // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                            Uri u = new Uri(new Uri(url), newUrl);
                            newUrl = u.ToString();
                        }
                        break;
                    default:
                        return newUrl;
                }
                url = newUrl;
            }
            catch (WebException)
            {
                // Return the last known good URL
                return newUrl;
            }
            catch (Exception ex)
            {
                return null;
            }
            finally
            {
                if (resp != null)
                    resp.Close();
            }
        } 
        while (maxRedirCount-- > 0);
            return newUrl;
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