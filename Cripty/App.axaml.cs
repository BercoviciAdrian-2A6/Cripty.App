using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cripty.ViewModels;
using Cripty.Views;

namespace Cripty;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel mainViewModel = new();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.Exit += (_, _) =>
            {
                mainViewModel.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}