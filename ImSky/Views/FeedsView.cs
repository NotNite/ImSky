using System.Numerics;
using ImGuiNET;
using ImSky.Api;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class FeedsView(
    GuiService gui,
    FeedService feed,
    ILogger<FeedsView> logger
) : View {
    private string? cursor;
    private Task? fetchingTask;

    public override void Draw() {
        foreach (var post in feed.Posts.ToList()) {
            // TODO: replies
            if (post.ReplyParent is not null || post.ReplyRoot is not null) continue;

            var y = ImGui.GetCursorPosY();

            if (post.UiState.TotalHeight is { } totalHeight) {
                var visibleStart = ImGui.GetScrollY();
                var visibleEnd = visibleStart + ImGui.GetWindowHeight();
                var offScreen = y + totalHeight < visibleStart || y > visibleEnd;
                if (offScreen) {
                    ImGui.Dummy(new Vector2(0, totalHeight));
                    ImGui.Separator();
                    continue;
                }
            }

            if (post.ReplyParent is not null || post.ReplyRoot is not null) {

            }

            Components.Post(post, gui);
            Components.PostInteraction(post, feed, logger);

            var newY = ImGui.GetCursorPosY();
            var newHeight = newY - y;
            post.UiState.TotalHeight = newHeight;

            ImGui.Separator();
        }

        ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 5));
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) this.FetchPosts();
    }

    public override void OnActivate() {
        this.FetchPosts();
    }

    private void FetchPosts() {
        if (this.fetchingTask?.IsCompleted == false) return;

        this.fetchingTask = Task.Run(async () => {
            try {
                this.cursor = await feed.FetchPosts(this.cursor);
                logger.LogDebug("Fetched posts, got cursor: {Cursor}", this.cursor);
                this.fetchingTask = null;
            } catch (Exception e) {
                logger.LogError(e, "Failed to get timeline");
            }
        });
    }
}
