using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

using Yndx.Models;

namespace Yndx.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager = new("Yndx.Resources.Strings", typeof(LocalizationService).Assembly);
    private CultureInfo _culture;
    private AppLanguage _currentLanguage;

    public static LocalizationService Instance { get; } = new();

    private LocalizationService()
    {
        _currentLanguage = DetectOsLanguage();
        _culture = CreateCulture(_currentLanguage);
        ApplyCulture(_culture);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppLanguage CurrentLanguage => _currentLanguage;

    public string this[string key] => _resourceManager.GetString(key, _culture) ?? key;

    public void Initialize(AppLanguage? preferredLanguage)
    {
        SetLanguage(preferredLanguage ?? DetectOsLanguage());
    }

    public bool SetLanguage(AppLanguage language)
    {
        if (language == _currentLanguage)
        {
            return false;
        }

        _currentLanguage = language;
        _culture = CreateCulture(language);
        ApplyCulture(_culture);

        RaisePropertyChanged(nameof(CurrentLanguage));
        RaisePropertyChanged("Item[]");
        return true;
    }

    public AppLanguage DetectOsLanguage()
    {
        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;
    }

    public string Get(string key)
    {
        return this[key];
    }

    public string Format(string key, params object[] arguments)
    {
        return string.Format(_culture, Get(key), arguments);
    }

    private static CultureInfo CreateCulture(AppLanguage language)
    {
        return language == AppLanguage.Russian ? new CultureInfo("ru") : new CultureInfo("en");
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
