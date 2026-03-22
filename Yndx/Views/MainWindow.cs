using Avalonia.Controls;
using Avalonia.Media;

using Yndx.ViewModels;

namespace Yndx.Views;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        var viewModel = new MainViewModel();

        Title = "Yndx Music Browser";
        Width = 1440;
        Height = 920;
        MinWidth = 1100;
        MinHeight = 760;
        Background = new SolidColorBrush(Color.Parse("#F4EFE6"));
        Content = new MainView(viewModel);
    }
}
