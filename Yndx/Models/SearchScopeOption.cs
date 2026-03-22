namespace Yndx.Models;

public sealed class SearchScopeOption
{
    public required SearchScope Scope { get; init; }

    public required string Label { get; init; }

    public override string ToString() => Label;
}
