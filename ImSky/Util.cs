using ImGuiNET;

namespace ImSky;

public class Util {
    public static string FormatRelative(TimeSpan time) {
        if (time.TotalDays >= 1) return $"{time.Days}d";
        if (time.TotalHours >= 1) return $"{time.Hours}h";
        if (time.TotalMinutes >= 1) return $"{time.Minutes}m";
        return $"{time.Seconds}s";
    }

    public static bool DisabledButton(string label, bool disabled) {
        if (disabled) ImGui.BeginDisabled();
        var clicked = ImGui.Button(label);
        if (disabled) ImGui.EndDisabled();
        return clicked;
    }

    public static string StripWeirdCharacters(string str, bool unformatted = false) {
        if (unformatted) {
            str = str
                // bad imgui escapes
                .Replace("%", "%%");
        }

        return str
            // smart quotes
            .Replace("\u201c", "\"")
            .Replace("\u201d", "\"")
            .Replace("\u2018", "'");
    }
}
