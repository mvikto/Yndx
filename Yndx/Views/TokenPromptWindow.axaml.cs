using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Yndx.Services;

namespace Yndx.Views;

public partial class TokenPromptWindow : Window
{
    private readonly LocalizationService _localization = LocalizationService.Instance;

    public TokenPromptWindow() : this(null)
    {
    }

    public TokenPromptWindow(string? currentToken = null)
    {
        AvaloniaXamlLoader.Load(this);
        ApplyLocalization();
        _localization.PropertyChanged += OnLocalizationChanged;
        TokenTextBox.Text = currentToken ?? string.Empty;
        Opened += (_, _) => TokenTextBox.Focus();
        Closed += (_, _) => _localization.PropertyChanged -= OnLocalizationChanged;
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        var token = TokenTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorText.Text = _localization["TokenPromptEmptyError"];
            return;
        }

        Close(token);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close((string?)null);
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalizationService.CurrentLanguage) or "Item[]")
        {
            ApplyLocalization();
        }
    }

    private void ApplyLocalization()
    {
        Title = _localization["TokenPromptTitle"];
        TitleText.Text = _localization["TokenPromptWindowTitle"];
        BodyText.Text = _localization["TokenPromptBody"];
        TokenTextBox.Watermark = _localization["TokenPromptPlaceholder"];
        CancelButton.Content = _localization["TokenPromptCancel"];
        ConfirmButton.Content = _localization["TokenPromptConfirm"];
    }
}
