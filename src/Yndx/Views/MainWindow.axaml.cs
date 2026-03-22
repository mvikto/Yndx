using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Yndx.Models;
using Yndx.Services;
using Yndx.ViewModels;

namespace Yndx.Views;

public partial class MainWindow : Window
{
    private readonly LocalizationService _localization = LocalizationService.Instance;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Opened += OnOpened;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private async void OnOpened(object? sender, EventArgs e)
    {
        await ViewModel.InitializeAsync();
        await PromptForTokenChoiceAsync();
    }

    private async void EditToken_OnClick(object? sender, RoutedEventArgs e)
    {
        await PromptForTokenAsync();
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

    private async Task PromptForTokenChoiceAsync()
    {
        if (ViewModel.HasSavedToken)
        {
            var choice = await ShowSavedTokenDialogAsync();
            if (choice == true)
            {
                if (!await ViewModel.ConnectAsync())
                {
                    await PromptForTokenAsync();
                }

                return;
            }

            if (choice == false)
            {
                await PromptForTokenAsync();
                return;
            }

            Close();
            return;
        }

        await PromptForTokenAsync();
    }

    private async Task PromptForTokenAsync()
    {
        while (true)
        {
            var dialog = new TokenPromptWindow(ViewModel.Token);
            var token = await dialog.ShowDialog<string?>(this);

            if (string.IsNullOrWhiteSpace(token))
            {
                Close();
                return;
            }

            if (await ViewModel.SaveTokenAndConnectAsync(token))
            {
                return;
            }
        }
    }

    private async Task<bool?> ShowSavedTokenDialogAsync()
    {
        var dialog = new Window
        {
            Title = _localization["SavedTokenTitle"],
            Width = 500,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brush.Parse("#F4EFE6")
        };

        bool? result = null;

        var cancelButton = new Button
        {
            Content = _localization["SavedTokenCancel"],
            Background = Avalonia.Media.Brush.Parse("#A9A094"),
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 10)
        };
        cancelButton.Click += (_, _) =>
        {
            result = null;
            dialog.Close();
        };

        var replaceButton = new Button
        {
            Content = _localization["SavedTokenChange"],
            Background = Avalonia.Media.Brush.Parse("#A64B2A"),
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 10)
        };
        replaceButton.Click += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        var useSavedButton = new Button
        {
            Content = _localization["SavedTokenUse"],
            Background = Avalonia.Media.Brush.Parse("#0F766E"),
            Foreground = Avalonia.Media.Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 10)
        };
        useSavedButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        dialog.Content = new Grid
        {
            Margin = new Thickness(24),
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Children =
            {
                new TextBlock
                {
                    Text = _localization["SavedTokenQuestion"],
                    FontSize = 28,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    Foreground = Avalonia.Media.Brush.Parse("#14383B")
                },
                new TextBlock
                {
                    Text = _localization["SavedTokenBody"],
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = Avalonia.Media.Brush.Parse("#5C6667"),
                    Margin = new Thickness(0, 14, 0, 0)
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                    Children =
                    {
                        cancelButton,
                        replaceButton,
                        useSavedButton
                    }
                }
            }
        };

        Grid.SetRow(((Grid)dialog.Content).Children[0], 0);
        Grid.SetRow(((Grid)dialog.Content).Children[1], 1);
        Grid.SetRow(((Grid)dialog.Content).Children[2], 2);

        await dialog.ShowDialog(this);
        return result;
    }
}
