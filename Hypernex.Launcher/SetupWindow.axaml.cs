using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Hypernex.Launcher;

public partial class SetupWindow : Window
{
    public static Action<bool, string, string> OnClose = (submit, domain, location) => { };

    private bool didSubmit;

    private TextBox TargetDomain;
    private TextBox SelectedDirectory;
    
    public SetupWindow()
    {
        InitializeComponent();
        Closed += (sender, args) =>
        {
            if (!didSubmit)
                OnClose.Invoke(false, String.Empty, String.Empty);
        };
        TargetDomain = (TextBox) ((Canvas) Content).Children[2];
        SelectedDirectory = (TextBox) ((Canvas) Content).Children[5];
    }

    internal void SetConfig(LauncherCache launcherCache) => TargetDomain.Text = launcherCache.TargetDomain;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private string PromptFolder()
    {
        OpenFolderDialog openFolderDialog = new OpenFolderDialog();
        return openFolderDialog.ShowAsync(this).Result ?? String.Empty;
    }

    private void SelectDirectoryPressed(object? sender, RoutedEventArgs e) => SelectedDirectory.Text = PromptFolder();

    private void SubmitPressed(object? sender, RoutedEventArgs e)
    {
        if (Directory.Exists(SelectedDirectory.Text))
        {
            didSubmit = true;
            OnClose.Invoke(true, TargetDomain.Text, SelectedDirectory.Text);
        }
        Close();
    }
}