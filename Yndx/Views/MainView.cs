using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

using Avalonia.Markup.Declarative;

using Yndx.Models;
using Yndx.ViewModels;

namespace Yndx.Views;

public sealed class MainView : ViewBase<MainViewModel>
{
    private TextBox _tokenBox = null!;
    private TextBox _folderBox = null!;
    private TextBox _searchBox = null!;
    private ComboBox _scopeBox = null!;
    private TextBlock _accountText = null!;
    private TextBlock _statusText = null!;
    private Button _connectButton = null!;
    private Button _searchButton = null!;
    private StackPanel _resultsPanel = null!;
    private StackPanel _detailPanel = null!;
    private StackPanel _queuePanel = null!;

    public MainView(MainViewModel viewModel) : base(viewModel)
    {
    }

    protected override void OnCreated()
    {
        ViewModel!.PropertyChanged += (_, _) => Dispatcher.UIThread.Post(RefreshUi);
        _ = Dispatcher.UIThread.InvokeAsync(ViewModel.InitializeAsync);
    }

    protected override StyleGroup? BuildStyles() =>
    [
        new Style<Border>(x => x.Class("card"))
            .Background(Brush("#FFFDFC"))
            .BorderBrush(Brush("#D9CEC0"))
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(22))
            .Padding(new Thickness(20)),

        new Style<Border>(x => x.Class("soft-card"))
            .Background(Brush("#F8F2EA"))
            .CornerRadius(new CornerRadius(18))
            .Padding(new Thickness(14)),

        new Style<Border>(x => x.Class("note-card"))
            .Background(Brush("#E5DDD2"))
            .CornerRadius(new CornerRadius(18))
            .Padding(new Thickness(16)),

        new Style<Button>(x => x.Class("primary-button"))
            .Background(Brush("#0F766E"))
            .Foreground(Brushes.White)
            .Height(42)
            .CornerRadius(new CornerRadius(14))
            .FontWeight(FontWeight.SemiBold),

        new Style<Button>(x => x.Class("warm-button"))
            .Background(Brush("#A64B2A"))
            .Foreground(Brushes.White)
            .Height(42)
            .CornerRadius(new CornerRadius(14))
            .FontWeight(FontWeight.SemiBold),

        new Style<Button>(x => x.Class("muted-button"))
            .Background(Brush("#7B8471"))
            .Foreground(Brushes.White)
            .Height(42)
            .CornerRadius(new CornerRadius(14))
            .FontWeight(FontWeight.SemiBold),

        new Style<Button>(x => x.Class("small-button"))
            .Height(34)
            .Padding(new Thickness(14, 0))
            .CornerRadius(new CornerRadius(12))
            .Foreground(Brushes.White)
            .FontWeight(FontWeight.SemiBold),

        new Style<TextBlock>(x => x.Class("section-title"))
            .FontSize(24)
            .FontWeight(FontWeight.Bold)
            .Foreground(Brush("#12383D")),

        new Style<TextBlock>(x => x.Class("muted-text"))
            .Foreground(Brush("#5B6D70"))
            .TextWrapping(TextWrapping.Wrap),

        new Style<TextBlock>(x => x.Class("result-title"))
            .FontSize(16)
            .FontWeight(FontWeight.SemiBold)
            .Foreground(Brush("#1B2D32")),

        new Style<TextBlock>(x => x.Class("subtle-text"))
            .Foreground(Brush("#6A5F58"))
            .TextWrapping(TextWrapping.Wrap),

        new Style<TextBlock>(x => x.Class("body-text"))
            .Foreground(Brush("#5C6667"))
            .TextWrapping(TextWrapping.Wrap)
    ];

    protected override object Build(MainViewModel? vm) =>
        new Grid()
            .Cols("320,*")
            .Margin(new Thickness(24))
            .Children(
                BuildSidebar().Col(0),
                BuildWorkspace().Col(1).Margin(new Thickness(22, 0, 0, 0))
            );

    private Control BuildSidebar() =>
        new StackPanel()
            .Spacing(16)
            .Children(
                BuildSessionCard(),
                new Border()
                    .Classes("note-card")
                    .Child(
                        new TextBlock()
                            .Classes("body-text")
                            .Text("Users and videos are browse-only. Albums, playlists, artists, tracks, and podcast episodes resolve into downloadable tracks.")));

    private Control BuildSessionCard() =>
        BuildCard(
            "Session",
            "Token is saved in plain text because you asked for the simple version.",
            new TextBlock()
                .Ref(out _accountText)
                .FontSize(18)
                .FontWeight(FontWeight.SemiBold)
                .Foreground(Brush("#14383B"))
                .Text(() => ViewModel!.AccountDisplay),

            new TextBox()
                .Ref(out _tokenBox)
                .Watermark("Paste Yandex token")
                .AcceptsReturn(true)
                .TextWrapping(TextWrapping.Wrap)
                .MinHeight(110)
                .With(box => box.TextChanged += (_, _) => ViewModel!.Token = box.Text ?? string.Empty),

            new TextBox()
                .Ref(out _folderBox)
                .Watermark("Download folder")
                .MinHeight(36)
                .With(box => box.TextChanged += (_, _) => ViewModel!.DownloadFolder = box.Text ?? string.Empty),

            new Button()
                .Ref(out _connectButton)
                .Classes("primary-button")
                .Content(() => ViewModel!.IsConnected ? "Reconnect" : "Connect")
                .IsEnabled(() => !ViewModel!.IsBusy)
                .OnClick(async _ => await ViewModel!.ConnectAsync()),

            new TextBlock()
                .Ref(out _statusText)
                .Classes("body-text")
                .Text(() => ViewModel!.StatusMessage));

    private Control BuildWorkspace() =>
        new Grid()
            .Rows("Auto,*")
            .Children(
                BuildSearchRow().Row(0),
                BuildColumns().Row(1).Margin(new Thickness(0, 18, 0, 0))
            );

    private Control BuildSearchRow() =>
        new Grid()
            .Cols("*,200,140")
            .Children(
                new TextBox()
                    .Ref(out _searchBox)
                    .Watermark("Search tracks, albums, artists, playlists, podcasts, videos, users")
                    .MinHeight(42)
                    .HorizontalAlignment(Avalonia.Layout.HorizontalAlignment.Stretch)
                    .With(box => box.TextChanged += (_, _) => ViewModel!.SearchText = box.Text ?? string.Empty)
                    .Col(0),

                new ComboBox()
                    .Ref(out _scopeBox)
                    .Width(190)
                    .ItemsSource(Enum.GetValues<SearchScope>())
                    .Margin(new Thickness(12, 0, 0, 0))
                    .With(box => box.SelectionChanged += (_, _) =>
                    {
                        if (box.SelectedItem is SearchScope scope)
                        {
                            ViewModel!.SelectedScope = scope;
                        }
                    })
                    .Col(1),

                new Button()
                    .Ref(out _searchButton)
                    .Classes("warm-button")
                    .Content("Search")
                    .Margin(new Thickness(12, 0, 0, 0))
                    .IsEnabled(() => !ViewModel!.IsBusy && ViewModel.IsConnected)
                    .OnClick(async _ => await ViewModel!.SearchAsync())
                    .Col(2));

    private Control BuildColumns() =>
        new Grid()
            .Cols("1.15*,0.95*")
            .Children(
                new Grid()
                    .Rows("*,0.9*")
                    .Children(
                        BuildScrollCard(
                            "Results",
                            "Search hits from the selected Yandex scope.",
                            new StackPanel().Ref(out _resultsPanel).Spacing(10))
                            .Row(0),

                        BuildScrollCard(
                            "Queue",
                            "Downloads run one after another to keep things predictable.",
                            new StackPanel().Ref(out _queuePanel).Spacing(10))
                            .Row(1)
                            .Margin(new Thickness(0, 18, 0, 0)))
                    .Col(0),

                BuildScrollCard(
                        "Details",
                        "Inspect a result before downloading it.",
                        new StackPanel().Ref(out _detailPanel).Spacing(10))
                    .Col(1)
                    .Margin(new Thickness(18, 0, 0, 0)));

    private Border BuildCard(string title, string subtitle, params Control[] content) =>
        new Border()
            .Classes("card")
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Classes("section-title")
                            .Text(title),

                        new TextBlock()
                            .Classes("muted-text")
                            .Text(subtitle),

                        new StackPanel()
                            .Spacing(12)
                            .Children(content)));

    private Border BuildScrollCard(string title, string subtitle, Panel content) =>
        BuildCard(
            title,
            subtitle,
            new ScrollViewer().Content(content));

    private void RefreshUi()
    {
        if (_tokenBox is null)
        {
            return;
        }

        if (_tokenBox.Text != ViewModel!.Token)
        {
            _tokenBox.Text = ViewModel.Token;
        }

        if (_folderBox.Text != ViewModel.DownloadFolder)
        {
            _folderBox.Text = ViewModel.DownloadFolder;
        }

        if (_searchBox.Text != ViewModel.SearchText)
        {
            _searchBox.Text = ViewModel.SearchText;
        }

        _scopeBox.SelectedItem = ViewModel.SelectedScope;

        RebuildResults();
        RebuildDetails();
        RebuildQueue();
    }

    private void RebuildResults()
    {
        _resultsPanel.Children.Clear();

        if (ViewModel!.Results.Count == 0)
        {
            _resultsPanel.Children.Add(EmptyState("Run a search to load results."));
            return;
        }

        foreach (var item in ViewModel.Results)
        {
            var actionRow = new StackPanel()
                .Orientation(Avalonia.Layout.Orientation.Horizontal)
                .Spacing(10)
                .Children(
                    BuildSmallButton("Open", "#184A54", async () => await ViewModel.SelectResultAsync(item)),
                    BuildSmallButton(item.CanDownload ? "Download" : "Browse", item.CanDownload ? "#A64B2A" : "#7B8471", async () =>
                    {
                        if (item.CanDownload)
                        {
                            await ViewModel.DownloadResultAsync(item);
                        }
                        else
                        {
                            await ViewModel.SelectResultAsync(item);
                        }
                    }));

            _resultsPanel.Children.Add(
                new Border()
                    .Classes("soft-card")
                    .Child(
                        new StackPanel()
                            .Spacing(8)
                            .Children(
                                new TextBlock()
                                    .Classes("result-title")
                                    .Text(item.Title),
                                new TextBlock()
                                    .Classes("subtle-text")
                                    .Text(item.Subtitle),
                                new TextBlock()
                                    .Classes("body-text")
                                    .Text(item.Description),
                                actionRow)));
        }
    }

    private void RebuildDetails()
    {
        _detailPanel.Children.Clear();

        if (ViewModel!.Detail is null)
        {
            _detailPanel.Children.Add(EmptyState("Pick a result to inspect tracks or browse-only details."));
            return;
        }

        var detail = ViewModel.Detail;

        _detailPanel.Children.Add(
            new TextBlock()
                .FontSize(22)
                .FontWeight(FontWeight.Bold)
                .Foreground(Brush("#14383B"))
                .Text(detail.Title));

        _detailPanel.Children.Add(
            new TextBlock()
                .Classes("subtle-text")
                .FontWeight(FontWeight.SemiBold)
                .Text(detail.Subtitle));

        _detailPanel.Children.Add(
            new TextBlock()
                .Classes("body-text")
                .Text(detail.Description));

        if (detail.CanDownloadAll)
        {
            _detailPanel.Children.Add(BuildSmallButton("Download all", "#0F766E", async () => await ViewModel.QueueTracksAsync(detail.Tracks, detail.FolderHint)));
        }

        if (detail.Tracks.Count == 0)
        {
            _detailPanel.Children.Add(EmptyState(detail.IsBrowseOnly ? "This result can be browsed but not downloaded." : "No tracks were returned."));
            return;
        }

        foreach (var track in detail.Tracks)
        {
            _detailPanel.Children.Add(
                new Border()
                    .Classes("soft-card")
                    .Child(
                        new Grid()
                            .Cols("*,120")
                            .Children(
                                new StackPanel()
                                    .Spacing(4)
                                    .Children(
                                        new TextBlock()
                                            .Classes("result-title")
                                            .Text(track.Title),

                                        new TextBlock()
                                            .Classes("subtle-text")
                                            .Text(track.Subtitle),

                                        new TextBlock()
                                            .Classes("body-text")
                                            .Text(track.Album))
                                    .Col(0),

                                BuildSmallButton("Queue track", "#A64B2A", async () => await ViewModel.QueueSingleTrackAsync(track.Track, detail.FolderHint))
                                    .Col(1)
                                    .Margin(new Thickness(12, 0, 0, 0)))));
        }
    }

    private void RebuildQueue()
    {
        _queuePanel.Children.Clear();

        if (ViewModel!.Queue.Count == 0)
        {
            _queuePanel.Children.Add(EmptyState("Queued downloads will appear here."));
            return;
        }

        foreach (var item in ViewModel.Queue)
        {
            _queuePanel.Children.Add(
                new Border()
                    .Classes("soft-card")
                    .Child(
                        new StackPanel()
                            .Spacing(4)
                            .Children(
                                new TextBlock()
                                    .Classes("result-title")
                                    .Text(item.Title),

                                new TextBlock()
                                    .Classes("subtle-text")
                                    .Text(item.Subtitle),

                                new TextBlock()
                                    .Foreground(Brush(StatusBrush(item.Status)))
                                    .FontWeight(FontWeight.SemiBold)
                                    .Text(item.Status),

                                new TextBlock()
                                    .Classes("body-text")
                                    .Text(string.IsNullOrWhiteSpace(item.TargetPath) ? item.FolderHint : item.TargetPath),

                                string.IsNullOrWhiteSpace(item.Error)
                                    ? EmptyState(string.Empty, false)
                                    : new TextBlock()
                                        .Foreground(Brush("#B42318"))
                                        .TextWrapping(TextWrapping.Wrap)
                                        .Text(item.Error))));
        }
    }

    private Button BuildSmallButton(string title, string background, Func<Task> action) =>
        new Button()
            .Classes("small-button")
            .Background(Brush(background))
            .HorizontalAlignment(Avalonia.Layout.HorizontalAlignment.Left)
            .Content(title)
            .OnClick(async _ => await action());

    private static Control EmptyState(string text, bool showWhenEmpty = true)
    {
        if (!showWhenEmpty && string.IsNullOrWhiteSpace(text))
        {
            return new Border().Height(0);
        }

        return new Border()
            .Background(Brush("#EFE7DB"))
            .CornerRadius(new CornerRadius(14))
            .Padding(new Thickness(12))
            .Child(
                new TextBlock()
                    .Foreground(Brush("#6A5F58"))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(text));
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    private static string StatusBrush(string status) => status switch
    {
        "Done" => "#0F766E",
        "Failed" => "#B42318",
        "Downloading" => "#A64B2A",
        _ => "#5C6667"
    };
}
