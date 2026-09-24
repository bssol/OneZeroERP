using Microsoft.JSInterop;
using OneZeroErp.Application;

namespace OneZeroErp.Web;

public sealed class ThemeService(IJSRuntime js) : IThemeService
{
    public IReadOnlyList<ThemeDefinition> Themes { get; } =
    [
        new("professional", "Professional Blue / Indigo", "Focused, familiar, and confident."),
        new("slate", "Neutral Slate", "Calm, restrained, and information-dense."),
        new("emerald", "Emerald / Teal", "Fresh, positive, and operational.")
    ];

    public string CurrentTheme { get; private set; } = "professional";

    public async Task SetThemeAsync(string themeKey)
    {
        if (Themes.All(x => x.Key != themeKey)) return;
        CurrentTheme = themeKey;
        await js.InvokeVoidAsync("oneZeroTheme.set", themeKey);
    }
}
