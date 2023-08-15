using System;
using System.IO;
using Tomlet;
using Tomlet.Attributes;
using Tomlet.Models;

namespace Hypernex.Launcher;

public class LauncherCache
{
    public static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hypernex.Launcher");

    private static string ConfigLocation => Path.Combine(CacheDirectory, "launcher.cfg");
    
    public static string SaveFile(byte[] data, string fileName)
    {
        if (!Directory.Exists(CacheDirectory))
            Directory.CreateDirectory(CacheDirectory);
        string file = Path.Combine(CacheDirectory, fileName);
        FileStream fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);
        fs.Write(data, 0, data.Length);
        fs.Dispose();
        return file;
    }

    public static LauncherCache Create()
    {
        if (!Directory.Exists(CacheDirectory))
            Directory.CreateDirectory(CacheDirectory);
        if (File.Exists(ConfigLocation))
        {
            try
            {
                string t = File.ReadAllText(ConfigLocation);
                LauncherCache l = TomletMain.To<LauncherCache>(t);
                return l;
            }
            catch (Exception)
            {
                return new LauncherCache();
            }
        }
        return new LauncherCache();
    }

    internal void Save()
    {
        TomlDocument document = TomletMain.DocumentFrom(typeof(LauncherCache), this);
        string text = document.SerializedValue;
        File.WriteAllText(ConfigLocation, text);
    }

    [TomlProperty("TargetDomain")]
    public string TargetDomain { get; set; } = "play.hypernex.dev";
    
    [TomlProperty("InstallDirectory")]
    public string InstallDirectory { get; set; } = String.Empty;
}