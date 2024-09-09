using System.Numerics;
using FishyFlip.Models;
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
    private bool fetchedFeeds;

    public override void Draw() {
        Components.MenuBar(() => {
            List<string> items = ["Following"];
            items.AddRange(feed.Feeds.Select(f => f.Title));
            var itemsArray = items.ToArray();
            var selected = feed.Feed is null ? 0 : feed.Feeds.IndexOf(feed.Feed) + 1;

            var disabled = this.fetchingTask?.IsCompleted == false;
            if (disabled) ImGui.BeginDisabled();
            if (ImGui.Combo("##feeds_combo", ref selected, itemsArray, itemsArray.Length)) {
                this.cursor = null;
                feed.Posts.Clear();
                feed.Feed = selected == 0 ? null : feed.Feeds[selected - 1];
                this.FetchPosts();
            }
            if (disabled) ImGui.EndDisabled();
        }, goBack: false);

        if (ImGui.BeginChild("##feeds", Vector2.Zero)) {
            var visibleStart = ImGui.GetScrollY();
            var visibleEnd = visibleStart + ImGui.GetWindowHeight();
            //logger.LogDebug("visibleStart: {VisibleStart}, visibleEnd: {VisibleEnd}", visibleStart, visibleEnd);

            foreach (var post in feed.Posts.ToList()) {
                var y = ImGui.GetCursorPosY();
                var totalHeight = post.UiState.TotalHeight;
                var offScreen = totalHeight is not null && (y + totalHeight < visibleStart || y > visibleEnd);
                //logger.LogDebug("y: {Y}, offScreen: {OffScreen}", y, offScreen);

                if (post.ReplyRoot is not null) {
                    Components.Post(post.ReplyRoot, gui, skipContent: offScreen);
                    if (!offScreen) Components.PostInteraction(post.ReplyRoot, feed, logger);
                }
                if (post.ReplyParent is not null && post.ReplyParent.PostId != post.ReplyRoot?.PostId) {
                    Components.Post(post.ReplyParent, gui, skipContent: offScreen);
                    if (!offScreen) Components.PostInteraction(post.ReplyParent, feed, logger);
                }

                Components.Post(post, gui, skipContent: offScreen);
                if (!offScreen) Components.PostInteraction(post, feed, logger);

                var newY = ImGui.GetCursorPosY();
                var newHeight = newY - y;
                if (offScreen) {
                    ImGui.Dummy(new Vector2(0, totalHeight!.Value - newHeight));
                } else {
                    post.UiState.TotalHeight = newHeight;
                }

                ImGui.Separator();
            }

            ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 5));
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) this.FetchPosts();

            ImGui.EndChild();
        }
    }

    public override void OnActivate() {
        this.FetchPosts();
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

            if (!this.fetchedFeeds) {
                try {
                    await feed.FetchFeeds();
                } catch (Exception e) {
                    logger.LogError(e, "Failed to get feeds");
                }

                this.fetchedFeeds = true;
            }

            // sleep for a bit as a cooldown
            await Task.Delay(1000);
        });
    }
}
