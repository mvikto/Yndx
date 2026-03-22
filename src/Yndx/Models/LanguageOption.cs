namespace Yndx.Models;

public sealed class LanguageOption
{
    public required AppLanguage Language { get; init; }

    public required string Label { get; init; }

    public override string ToString() => Label;
}
