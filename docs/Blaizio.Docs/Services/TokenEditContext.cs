using Blaizio.Cli.Core.Styling;

namespace Blaizio.Docs.Services;

/// <summary>
/// Everything the dock's token popover needs the moment it opens: the resolved mode, the token's
/// current effective color (override or the theme's own computed value) and the background /
/// foreground partners its contrast readout grades against.
/// </summary>
public sealed record TokenEditContext(
    string Token, bool IsDark, OklchColor Value, OklchColor Background, OklchColor Foreground);
