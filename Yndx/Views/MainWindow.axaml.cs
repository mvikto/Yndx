using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Yndx.Models;
using Yndx.ViewModels;

namespace Yndx.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Opened += async (_, _) => await ViewModel.InitializeAsync();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void ConnectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.ConnectAsync();
    }

    private async void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.SearchAsync();
    }

    private async void OpenResult_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SearchResultItem item })
        {
            await ViewModel.SelectResultAsync(item);
        }
    }

    private async void DownloadResult_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SearchResultItem item })
        {
            await ViewModel.DownloadResultAsync(item);
        }
    }

    private async void DownloadAll_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.QueueCurrentDetailAsync();
    }

    private async void QueueTrack_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TrackEntry track })
        {
            await ViewModel.QueueTrackEntryAsync(track);
        }
    }
}
