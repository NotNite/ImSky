using System.Numerics;
using ImGuiNET;
using ImSky.Api;
using ImSky.Models;

namespace ImSky.Views;

public class UserView(
    GuiService gui,
    UsersService users
) : View {
    public string? Handle;
    public View? Parent;

    private User? user;
    private Task? lookupTask;

    private void Retreat() {
        if (this.Parent is not null) {
            gui.SetView(this.Parent);
        } else {
            gui.SetView<FeedsView>();
        }
    }

    public override void Draw() {
        if (Components.MenuBar(() => ImGui.TextUnformatted("User"))) {
            this.Retreat();
            return;
        }

        if (this.lookupTask?.IsFaulted == true) {
            ImGui.TextColored(Colors.Red, this.lookupTask.Exception?.ToString());
            return;
        }

        if (this.user is null) {
            ImGui.TextUnformatted("Loading...");
            return;
        }

        var width = ImGui.GetContentRegionAvail().X;
        const float ratio = 1500 / 500f;
        if (this.user.Banner is not null) {
            var banner = gui.GetTexture(this.user.Banner);
            var bannerSize = new Vector2(width, width / ratio);
            banner.Draw(bannerSize);
        } else {
            ImGui.Dummy(new Vector2(width, width / ratio));
        }

        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var spacing = style.ItemSpacing.Y;
        var size = (lineHeight * 2) + spacing;

        var avatar = gui.GetTexture(this.user.AvatarUrl);
        avatar.Draw(new Vector2(size, size));
        ImGui.SameLine();

        var cursor = ImGui.GetCursorPos();
        var handleStr = "@" + this.user.Handle;
        ImGui.TextUnformatted(this.user.DisplayName ?? handleStr);
        ImGui.SetCursorPos(cursor with {Y = cursor.Y + lineHeight});
        ImGui.TextColored(Colors.Grey, handleStr);

        ImGui.TextWrapped(Util.StripWeirdCharacters(this.user.Description ?? string.Empty));

        ImGui.Separator();

        // TODO: Implement user posts
    }

    public override void OnActivate() {
        this.lookupTask = Task.Run(async () => {
            if (this.Handle is null) throw new Exception("Handle is null");
            this.user = await users.GetUserProfile(this.Handle);
        });
    }
}
