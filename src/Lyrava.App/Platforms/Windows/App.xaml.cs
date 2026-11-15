using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Lyrava.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => Lyrava.App.MauiProgram.CreateMauiApp();
}
