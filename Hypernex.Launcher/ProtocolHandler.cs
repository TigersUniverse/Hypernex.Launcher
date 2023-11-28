using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using Avalonia.Controls;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Microsoft.Win32;

namespace Hypernex.Launcher;

[SupportedOSPlatform("Windows")]
public static class ProtocolHandler
{
    private const string PROTOCOL_KEY = "hypernex";
    
    public static async Task Register(Window ownerWindow)
    {
        string? pathToHypernex = Process.GetCurrentProcess().MainModule?.FileName;
        if(pathToHypernex == null)
            return;
        RegistryKey root = Registry.ClassesRoot;
        if (!root.GetSubKeyNames().Contains(PROTOCOL_KEY))
            await Create(ownerWindow, root, pathToHypernex);
        else
        {
            RegistryKey hypernexKey = root.OpenSubKey(PROTOCOL_KEY)!;
            hypernexKey.SetValue("URL Protocol", "", RegistryValueKind.String);
            RegistryKey? shell = hypernexKey.OpenSubKey("shell");
            if(shell == null)
            {
                await Create(ownerWindow, root, pathToHypernex);
                return;
            }
            RegistryKey? open = shell.OpenSubKey("open");
            if(open == null)
            {
                await Create(ownerWindow, root, pathToHypernex);
                return;
            }
            RegistryKey? command = open.OpenSubKey("command");
            if(command == null)
            {
                await Create(ownerWindow, root, pathToHypernex);
                return;
            }
            object? v = command.GetValue(null);
            if (v == null)
                await Create(ownerWindow, root, pathToHypernex);
        }
    }
    
    // https://stackoverflow.com/a/1089061
    private static bool IsUserAdministrator()
    {
        //bool value to hold our return value
        bool isAdmin;
        WindowsIdentity user = null;
        try
        {
            //get the currently logged in user
            user = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(user);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (UnauthorizedAccessException ex)
        {
            isAdmin = false;
        }
        catch (Exception ex)
        {
            isAdmin = false;
        }
        finally
        {
            if (user != null)
                user.Dispose();
        }
        return isAdmin;
    }

    private static async Task Create(Window ownerWindow, RegistryKey root, string pathToHypernex)
    {
        if (!IsUserAdministrator())
        {
            ButtonResult buttonResult = await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = "Insufficient Permissions",
                ContentMessage = "To register Hypernex as a URL protocol, you must run as admin.\nWould you like to restart as admin?",
                WindowIcon = new WindowIcon(AssetTools.Icon),
                Icon = Icon.Question,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            }).Show(ownerWindow);
            if(buttonResult == ButtonResult.Yes)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pathToHypernex,
                    WorkingDirectory = Path.GetDirectoryName(AppContext.BaseDirectory),
                    UseShellExecute = true,
                    Verb = "runas"
                };
                foreach (string arg in Environment.GetCommandLineArgs())
                    startInfo.ArgumentList.Add(arg);
                new Process {StartInfo = startInfo}.Start();
                Environment.Exit(0);
                return;
            }
            return;
        }
        RegistryKey hypernexKey = root.CreateSubKey(PROTOCOL_KEY);
        hypernexKey.SetValue("URL Protocol", "", RegistryValueKind.String);
        RegistryKey command = hypernexKey.CreateSubKey("shell").CreateSubKey("open").CreateSubKey("command");
        command.SetValue(null, $"\"{pathToHypernex}\" \"%1\"");
    }
}