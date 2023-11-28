using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace Hypernex.Launcher;

public partial class MainWindow : Window
{
    private readonly bool IsFirstOpen;
    private readonly bool Uninstall;
    private FileStream? gifStream;
    private SetupWindow? SetupWindow;
    
    public MainWindow()
    {
        InitializeComponent();
        AssetTools.Init();
        LauncherCache launcherCache = LauncherCache.Create();
        Uninstall = Environment.GetCommandLineArgs().Contains("--uninstall");
        IsFirstOpen = string.IsNullOrEmpty(launcherCache.InstallDirectory);
        try { DisplayRandomImage(launcherCache); } catch(Exception){ }
        if (IsFirstOpen)
        {
            SetupWindow = new SetupWindow();
            SetupWindow.SetConfig(launcherCache);
            SetupWindow.OnClose += (sumbit, domain, location) =>
            {
                if (!sumbit)
                {
                    Environment.Exit(0);
                    return;
                }
                launcherCache.TargetDomain = domain;
                launcherCache.InstallDirectory = location;
                launcherCache.Save();
                Launch(launcherCache);
            };
        }
        Opened += async (sender, args) =>
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                try{ await ProtocolHandler.Register(this); } catch(Exception){ }
            if (Uninstall && !IsFirstOpen)
            {
                ActionText.Text = "Uninstalling";
                ProgressBar.Value = 0;
                if (Directory.Exists(LauncherCache.CacheDirectory))
                    Directory.Delete(LauncherCache.CacheDirectory, true);
                bool installLocationEmpty = string.IsNullOrEmpty(launcherCache.InstallDirectory);
                if(!installLocationEmpty && Directory.Exists(launcherCache.InstallDirectory))
                    Directory.Delete(launcherCache.InstallDirectory, true);
                ActionText.Text = "Uninstalled";
                ProgressBar.Value = 100;
                new Thread(() =>
                {
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                }).Start();
                return;
            }
            SetupWindow?.Show(this);
            if(!IsFirstOpen)
                Launch(launcherCache);
        };
    }

    private void DisplayRandomImage(LauncherCache launcherCache)
    {
        byte[] data = ImageTools.GetRandomImage(launcherCache.TargetDomain);
        if (ImageTools.IsGif(data))
        {
            string gifFile = ImageTools.SaveGif(data);
            gifStream = File.OpenRead(gifFile);
            GifVector.SourceStream = gifStream;
            GifVector.IsVisible = true;
            ImageVector.IsVisible = false;
            return;
        }
        using MemoryStream ms = new MemoryStream(data);
        Bitmap bitmap = new Bitmap(ms);
        ImageVector.Source = bitmap;
        ImageVector.IsVisible = true;
        GifVector.IsVisible = false;
    }

    private string GetArgs()
    {
        StringBuilder s = new StringBuilder();
        foreach (string commandLineArg in Environment.GetCommandLineArgs())
        {
            s.Append(commandLineArg);
            s.Append(" ");
        }
        return s.ToString();
    }

    private void Launch(LauncherCache launcherCache)
    {
        try
        {
            Installer.Install((didDownload, executableToLaunch) => Dispatcher.UIThread.InvokeAsync(() =>
            {
                ActionText.Text = "Launching";
                ProgressBar.Value = 100;
                new Thread(() =>
                {
                    ProcessStartInfo processStartInfo = new()
                    {
                        FileName = Path.GetFileName(executableToLaunch),
                        WorkingDirectory = Path.GetDirectoryName(executableToLaunch),
                        UseShellExecute = true,
                        Arguments = GetArgs()
                    };
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Make sure the file is executable first
                        Process chmodProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo("chmod", $"+x {executableToLaunch}")
                            {
                                CreateNoWindow = true
                            },
                            EnableRaisingEvents = true
                        };
                        chmodProcess.Exited += (sender, args) => { Process.Start(processStartInfo); };
                        chmodProcess.Start();
                    }
                    else
                        Process.Start(processStartInfo);
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                }).Start();
            }), (text, progress) => Dispatcher.UIThread.InvokeAsync(() =>
            {
                ActionText.Text = text;
                ProgressBar.Value = progress;
            }), launcherCache);
        }
        catch (Exception e)
        {
            MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.Ok,
                ContentTitle = "Failed to Launch Hypernex",
                ContentMessage = "Could not launch Hypernex with Exception: " + e,
                WindowIcon = new WindowIcon(AssetTools.Icon),
                Icon = MessageBox.Avalonia.Enums.Icon.Error,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            }).Show(this);
            Environment.Exit(0);
        }
    }
}