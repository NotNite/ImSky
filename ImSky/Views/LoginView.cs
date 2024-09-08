using ImGuiNET;
using ImSky.Api;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class LoginView(
    GuiService gui,
    Config config,
    AtProtoService atProto,
    ILogger<LoginView> logger
) : View {
    private string handle = config.Handle ?? string.Empty;
    private string password = string.Empty;
    private Task? loginTask;

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
        if (ImGui.Checkbox("Stay logged in", ref config.SaveSession)) config.Save();

        if (this.loginTask?.IsFaulted == true) {
            ImGui.TextColored(Colors.Red, this.loginTask.Exception?.ToString());
        }
    }

    private void Login() {
        config.Handle = this.handle;
        config.Save();

        this.loginTask = Task.Run(async () => {
            var canSessionLogin = config.Session is not null;
            var canPasswordLogin = !string.IsNullOrWhiteSpace(this.handle) && !string.IsNullOrWhiteSpace(this.password);

            if (canSessionLogin) {
                try {
                    var session = await atProto.LoginWithSession(config.Session!);
                    config.Session = session;
                    config.Save();

                    gui.SetView<FeedsView>();
                    return;
                } catch (Exception e) {
                    logger.LogWarning(e, "Failed to login with session");
                    // Rethrow only if we can't login with password so the error pops up in the UI
                    throw;
                }
            }

            if (canPasswordLogin) {
                // Don't try/catch here so the error pops up in the UI
                var session = await atProto.LoginWithPassword(this.handle, this.password);
                config.Session = session;
                config.Save();

                gui.SetView<FeedsView>();
            }
        });
    }

    public override void OnActivate() {
        if (config.AutoLogin) this.Login();
    }
}
