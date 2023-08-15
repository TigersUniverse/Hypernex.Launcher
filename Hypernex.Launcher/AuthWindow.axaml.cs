using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HypernexSharp;
using HypernexSharp.APIObjects;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;

namespace Hypernex.Launcher;

public partial class AuthWindow : Window
{
    private Action<bool, string, string, AuthWindow> result;
    private HypernexObject HypernexObject;
    private bool invoked;
    
    private TextBox Username;
    private TextBox Password;
    
    public AuthWindow()
    {
        InitializeComponent();
        Username = (TextBox) ((Canvas) Content).Children[1];
        Password = (TextBox) ((Canvas) Content).Children[2];
    }

    internal AuthWindow SetCallback(Action<bool, string, string, AuthWindow> r, HypernexObject hypernexObject)
    {
        result = r;
        HypernexObject = hypernexObject;
        Closed += (sender, args) =>
        {
            if(invoked) return;
            result.Invoke(false, String.Empty, String.Empty, this);
        };
        return this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Get2FACode(Action<string> o)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            MessageWindowResultDTO w = await MessageBoxManager.GetMessageBoxInputWindow(new MessageBoxInputParams
            {
                Multiline = false,
                ButtonDefinitions = new ButtonDefinition[1]
                {
                    new ButtonDefinition()
                    {
                        Name = "OK",
                        IsCancel = false,
                        IsDefault = true
                    }
                },
                ContentTitle = "Enter 2FA",
                ContentMessage = "Please enter your 2FA Code",
                WindowIcon = new WindowIcon(AssetTools.Icon),
                Icon = MessageBox.Avalonia.Enums.Icon.Question,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            }).Show(this);
            o.Invoke(w.Message);
        });
    }

    private void Login(string username, string password, string twofacode = "")
    {
        HypernexSettings hypernexSettings = new HypernexSettings(username, password, twofacode){ TargetDomain = HypernexObject.Settings.TargetDomain };
        HypernexObject o = new HypernexObject(hypernexSettings);
        o.Login(async r =>
        {
            if (!r.success)
            {
                invoked = true;
                await Dispatcher.UIThread.InvokeAsync(() => result.Invoke(false, String.Empty, String.Empty, this));
                return;
            }
            switch (r.result.Result)
            {
                case LoginResult.Missing2FA:
                    Get2FACode(code => Login(username, password, code));
                    break;
                case LoginResult.Correct:
                    o.GetUser(r.result.Token, async userResult =>
                    {
                        if (!userResult.success)
                        {
                            invoked = true;
                            await Dispatcher.UIThread.InvokeAsync(() => result.Invoke(false, String.Empty, String.Empty, this));
                            return;
                        }
                        invoked = true;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                            result.Invoke(true, userResult.result.UserData.Id, r.result.Token.content, this));
                    });
                    break;
                default:
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
                    {
                        ButtonDefinitions = ButtonEnum.Ok,
                        ContentTitle = "Invalid Auth",
                        ContentMessage = "Failed to Login! Cannot update.",
                        WindowIcon = new WindowIcon(AssetTools.Icon),
                        Icon = MessageBox.Avalonia.Enums.Icon.Error,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    }).Show(this);
                    invoked = true;
                    await Dispatcher.UIThread.InvokeAsync(() => result.Invoke(false, String.Empty, String.Empty, this));
                    break;
            }
        });
    }

    private void OnLoginClicked(object? sender, RoutedEventArgs e) => Login(Username.Text, Password.Text);
}