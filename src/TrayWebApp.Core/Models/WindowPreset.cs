namespace TrayWebApp.Core.Models;

/// <summary>
/// Predefined window size presets for quick switching.
/// </summary>
public class WindowPreset
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Built-in presets matching common device sizes</summary>
    public static readonly WindowPreset[] Defaults = new[]
    {
        new WindowPreset { Name = "모바일", Width = 390, Height = 844 },
        new WindowPreset { Name = "컴팩트", Width = 430, Height = 720 },
        new WindowPreset { Name = "태블릿", Width = 768, Height = 1024 },
        new WindowPreset { Name = "와이드", Width = 1024, Height = 768 },
        new WindowPreset { Name = "데스크톱", Width = 1280, Height = 800 },
    };

    public override string ToString() => $"{Name} ({Width}x{Height})";
}
