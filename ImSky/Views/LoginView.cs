using Hexa.NET.ImGui;
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

    public bool IsInitial;

    public override void Draw() {
        if (ImGui.InputText("PDS", ref config.Pds, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
            atProto.AtProtocol = AtProtoService.BuildProtocol(config.Pds);
            config.Save();
        }

        ImGui.InputText("Handle", ref this.handle, 256);
        ImGui.InputText("Password", ref this.password, 256, ImGuiInputTextFlags.Password);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use an app password for extra safety.");

        var disable = string.IsNullOrWhiteSpace(this.handle)
                      || string.IsNullOrWhiteSpace(this.password)
                      || this.loginTask?.IsCompleted == false;
        if (disable) ImGui.BeginDisabled();
        if (ImGui.Button("Login")) this.Login();
        if (disable) ImGui.EndDisabled();

        if (ImGui.Checkbox("Auto login", ref config.AutoLogin)) config.Save();

        if (this.loginTask?.IsFaulted == true) {
            ImGui.TextColored(Colors.Red, this.loginTask.Exception?.ToString());
        }
    }

    private void Login() {
        config.Handle = this.handle;
        config.Password = this.password;
        config.Save();

        this.loginTask = Task.Run(async () => {
            var canPasswordLogin = !string.IsNullOrWhiteSpace(this.handle) && !string.IsNullOrWhiteSpace(this.password);

            if (canPasswordLogin) {
                // Don't try/catch here so the error pops up in the UI
                await atProto.LoginWithPassword(this.handle, this.password);
                gui.SetView<FeedsView>();
            }
        });
    }

    public override void OnActivate() {
        if (config.AutoLogin && this.IsInitial) this.Login();
    }
}
