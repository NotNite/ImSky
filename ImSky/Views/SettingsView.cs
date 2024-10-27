using Hexa.NET.ImGui;

namespace ImSky.Views;

public class SettingsView(Config config) : View {
    public override void Draw() {
        Components.Hamburger();
        ImGui.SameLine();
        Components.MenuBar(() => ImGui.TextUnformatted("Settings"), goBack: false);

        var postWithoutLanguage = config.Language is null;
        if (ImGui.Checkbox("Post without a language", ref postWithoutLanguage)) {
            config.Language = postWithoutLanguage ? null : Config.DefaultLanguage;
            config.Save();
        }

        if (postWithoutLanguage) ImGui.BeginDisabled();

        if (ImGui.InputText("Language", ref config.Language, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
            config.Save();
        }
    }
}
