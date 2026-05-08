using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace TrayWebApp.App;

internal static class ThemeManager
{
    private sealed record ThemePalette(
        Color Primary,
        Color PrimaryHover,
        Color PrimaryPressed,
        Color Accent,
        Color Background,
        Color Surface,
        Color SurfaceAlt,
        Color SurfaceHover,
        Color TextPrimary,
        Color TextSecondary,
        Color TextMuted,
        Color Border,
        Color Danger,
        Color DangerBackground,
        Color DangerHover);

    private static readonly ThemePalette Dark = new(
        Primary: Color.FromRgb(0, 120, 212),
        PrimaryHover: Color.FromRgb(26, 138, 230),
        PrimaryPressed: Color.FromRgb(0, 90, 158),
        Accent: Color.FromRgb(0, 210, 255),
        Background: Color.FromRgb(30, 30, 46),
        Surface: Color.FromRgb(45, 45, 63),
        SurfaceAlt: Color.FromRgb(37, 37, 56),
        SurfaceHover: Color.FromRgb(61, 61, 82),
        TextPrimary: Colors.White,
        TextSecondary: Color.FromRgb(184, 184, 202),
        TextMuted: Color.FromRgb(126, 126, 148),
        Border: Color.FromRgb(76, 76, 104),
        Danger: Color.FromRgb(255, 100, 100),
        DangerBackground: Color.FromRgb(74, 30, 30),
        DangerHover: Color.FromRgb(106, 46, 46));

    private static readonly ThemePalette Light = new(
        Primary: Color.FromRgb(0, 95, 184),
        PrimaryHover: Color.FromRgb(20, 115, 204),
        PrimaryPressed: Color.FromRgb(0, 73, 143),
        Accent: Color.FromRgb(0, 95, 184),
        Background: Color.FromRgb(248, 249, 252),
        Surface: Colors.White,
        SurfaceAlt: Color.FromRgb(239, 242, 247),
        SurfaceHover: Color.FromRgb(229, 234, 242),
        TextPrimary: Color.FromRgb(24, 28, 36),
        TextSecondary: Color.FromRgb(69, 78, 94),
        TextMuted: Color.FromRgb(103, 114, 132),
        Border: Color.FromRgb(200, 208, 220),
        Danger: Color.FromRgb(178, 43, 31),
        DangerBackground: Color.FromRgb(255, 235, 232),
        DangerHover: Color.FromRgb(255, 220, 216));

    public static string NormalizeThemeMode(string? mode)
    {
        return string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
    }

    public static bool IsLight(string? mode)
    {
        return string.Equals(NormalizeThemeMode(mode), "Light", StringComparison.OrdinalIgnoreCase);
    }

    public static void Apply(string? mode)
    {
        var palette = IsLight(mode) ? Light : Dark;
        var resources = Application.Current.Resources;

        SetBrush(resources, "PrimaryBrush", palette.Primary);
        SetBrush(resources, "PrimaryHoverBrush", palette.PrimaryHover);
        SetBrush(resources, "PrimaryPressedBrush", palette.PrimaryPressed);
        SetBrush(resources, "AccentBrush", palette.Accent);
        SetBrush(resources, "BackgroundBrush", palette.Background);
        SetBrush(resources, "SurfaceBrush", palette.Surface);
        SetBrush(resources, "SurfaceAltBrush", palette.SurfaceAlt);
        SetBrush(resources, "SurfaceHoverBrush", palette.SurfaceHover);
        SetBrush(resources, "TextPrimaryBrush", palette.TextPrimary);
        SetBrush(resources, "TextSecondaryBrush", palette.TextSecondary);
        SetBrush(resources, "TextMutedBrush", palette.TextMuted);
        SetBrush(resources, "BorderBrush", palette.Border);
        SetBrush(resources, "DangerBrush", palette.Danger);
        SetBrush(resources, "DangerBackgroundBrush", palette.DangerBackground);
        SetBrush(resources, "DangerHoverBrush", palette.DangerHover);
    }

    public static Brush GetBrush(string key)
    {
        return Application.Current.Resources[key] as Brush ?? Brushes.Transparent;
    }

    public static System.Drawing.Color ToDrawingColor(string key)
    {
        if (GetBrush(key) is SolidColorBrush brush)
        {
            var color = brush.Color;
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        return System.Drawing.Color.Transparent;
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = new SolidColorBrush(color);
    }
}
