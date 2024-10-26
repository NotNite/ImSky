using System.Numerics;
using ImGuiNET;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class UserView(
    GuiService gui,
    UsersService users,
    FeedService feed,
    ILogger<UserView> logger
) : View {
    public string? Handle;
    public View? Parent;

    private User? user;
    private bool handleLikes;
    private Task? lookupTask;

    private string? cursor;
    private Task? fetchingTask;
    private bool bottom;

    private void Retreat() {
        if (this.Parent is not null) {
            gui.SetView(this.Parent);
        } else {
            gui.SetView<FeedsView>();
        }
    }

    public override void Draw() {
        Components.Hamburger();
        ImGui.SameLine();
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

        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
        ImGui.TextUnformatted(
            $"{this.user.FollowersCount} followers, {this.user.FollowingCount} following, {this.user.PostCount} posts");
        ImGui.PopStyleColor();

        if (ImGui.BeginTabBar("UserTabs")) {
            if (ImGui.BeginTabItem("Posts")) {
                if (this.handleLikes) {
                    this.handleLikes = false;
                    feed.Reset();
                    this.cursor = null;
                    this.FetchPosts();
                }

                Components.Posts(feed.Posts.ToList(), this.FetchPosts);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Likes")) {
                if (!this.handleLikes) {
                    this.handleLikes = true;
                    feed.Reset();
                    this.cursor = null;
                    this.FetchPosts();
                }

                Components.Posts(feed.Posts.ToList(), this.FetchPosts);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public override void OnActivate() {
        this.handleLikes = false;
        this.cursor = null;
        this.bottom = false;
        feed.Reset();
        this.lookupTask = Task.Run(async () => {
            if (this.Handle is null) throw new Exception("Handle is null");
            this.user = await users.GetUserProfile(this.Handle);
        });
    }

    private void FetchPosts() {
        if (this.fetchingTask?.IsCompleted == false) return;
        if (this.bottom) return;

        this.fetchingTask = Task.Run(async () => {
            logger.LogDebug("Fetching posts, cursor: {Cursor}", this.cursor);

            try {
                this.cursor = await feed.FetchUser(this.user!, this.handleLikes, this.cursor);
                logger.LogDebug("Fetched posts, got cursor: {Cursor}", this.cursor);
                this.fetchingTask = null;
                this.bottom = this.cursor is null;
            } catch (Exception e) {
                logger.LogError(e, "Failed to get timeline");
            }

            // sleep for a bit as a cooldown
            await Task.Delay(1000);
        });
    }
}
