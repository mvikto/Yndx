using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Yndx.Views;

public partial class TokenPromptWindow : Window
{
    public TokenPromptWindow() : this(null)
    {
    }

    public TokenPromptWindow(string? currentToken = null)
    {
        AvaloniaXamlLoader.Load(this);
        TokenTextBox.Text = currentToken ?? string.Empty;
        Opened += (_, _) => TokenTextBox.Focus();
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        var token = TokenTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorText.Text = "Token cannot be empty.";
            return;
        }

        Close(token);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close((string?)null);
    }
}
