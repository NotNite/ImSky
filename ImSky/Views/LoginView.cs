using ImGuiNET;
using ImSky.Api;

namespace ImSky.Views;

public class LoginView(
    GuiService gui,
    Config config,
    AtProtoService atProto
) : View {
    private string handle = config.Handle ?? string.Empty;
    private string password = config.Password ?? string.Empty;

    private Task? loginTask;

    public override void Draw() {
        ImGui.InputText("Handle", ref this.handle, 256);
        ImGui.InputText("Password", ref this.password, 256, ImGuiInputTextFlags.Password);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use an app password for extra safety.");

        var disable = string.IsNullOrWhiteSpace(this.handle) || string.IsNullOrWhiteSpace(this.password) ||
                      this.loginTask != null;
        if (disable) ImGui.BeginDisabled();
        if (ImGui.Button("Login")) this.Login();
        if (disable) ImGui.EndDisabled();

        ImGui.Checkbox("Auto login", ref config.AutoLogin);
        ImGui.Checkbox("Save password", ref config.SavePassword);

        if (this.loginTask?.IsFaulted == true) {
            ImGui.TextColored(Colors.Red, this.loginTask.Exception?.ToString());
        }
    }

    private void Login() {
        config.Handle = this.handle;
        config.Password = this.password;
        config.Save();

        this.loginTask = Task.Run(async () => {
            await atProto.LogIn(this.handle, this.password);
            gui.SetView<FeedsView>();
        });
    }

    public override void OnActivate() {
        if (config.AutoLogin
            && !string.IsNullOrWhiteSpace(config.Handle)
            && !string.IsNullOrWhiteSpace(config.Password)) {
            this.Login();
        }
    }
}
