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
    private Task? lookupTask;

    private string? cursor;
    private Task? fetchingTask;

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

        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
        ImGui.TextUnformatted($"{this.user.FollowersCount} followers, {this.user.FollowingCount} following, {this.user.PostCount} posts");
        ImGui.PopStyleColor();

        ImGui.Separator();

        // TODO: Implement user posts
        if (ImGui.BeginTabBar("UserTabs")) {
            if (ImGui.BeginTabItem("Posts")) {
                if (feed.FetchLikes) {
                    feed.FetchLikes = false;
                    feed.Posts.Clear();
                    this.cursor = null;
                    this.FetchPosts();
                }

                Components.Posts(feed.Posts.ToList(), this.FetchPosts);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Likes")) {
                if (!feed.FetchLikes) {
                    feed.FetchLikes = true;
                    feed.Posts.Clear();
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
        feed.Reset();
        this.lookupTask = Task.Run(async () => {
            if (this.Handle is null) throw new Exception("Handle is null");
            this.user = await users.GetUserProfile(this.Handle);
            feed.User = this.user;
            this.FetchPosts();
        });
    }

    private void FetchPosts() {
        if (this.fetchingTask?.IsCompleted == false) return;

        this.fetchingTask = Task.Run(async () => {
            logger.LogDebug("Fetching posts, cursor: {Cursor}", this.cursor);

            try {
                this.cursor = await feed.FetchPosts(this.cursor);
                logger.LogDebug("Fetched posts, got cursor: {Cursor}", this.cursor);
                this.fetchingTask = null;
            } catch (Exception e) {
                logger.LogError(e, "Failed to get timeline");
            }

            // sleep for a bit as a cooldown
            await Task.Delay(1000);
        });
    }
}
