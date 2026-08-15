using Avalonia.Controls;

namespace Cripty.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        global::Cripty.Services.CriptyInteraction.Attach(this);
    }
}
