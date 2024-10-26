using System.Numerics;
using ImGuiNET;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class FeedsView(
    GuiService gui,
    FeedService feed,
    ILogger<FeedsView> logger
) : View {
    private string? cursor;
    private Task? fetchingTask;
    private bool bottom;
    private bool fetchedFeeds;

    private List<Feed> feeds = new();
    private Feed? selectedFeed;

    public override void Draw() {
        if (Components.Hamburger()) return;
        ImGui.SameLine();
        Components.MenuBar(() => {
            List<string> items = ["Following"];
            items.AddRange(this.feeds.Select(f => f.Title));
            var itemsArray = items.ToArray();
            var selected = this.selectedFeed is null ? 0 : this.feeds.IndexOf(this.selectedFeed) + 1;

            const string postText = "Post";
            const string refreshText = "Refresh";

            var doubleFramePadding = ImGui.GetStyle().FramePadding * 2;
            var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
            var cra = ImGui.GetContentRegionAvail().X;

            var postSize = ImGui.CalcTextSize(postText) + doubleFramePadding;
            var refreshSize = ImGui.CalcTextSize(refreshText) + doubleFramePadding;
            var totalSize = postSize + refreshSize + new Vector2(itemSpacing, 0);
            var feedsComboSize = new Vector2(cra - totalSize.X - itemSpacing, 0);

            var disabled = this.fetchingTask?.IsCompleted == false;
            if (disabled) ImGui.BeginDisabled();

            ImGui.SetNextItemWidth(feedsComboSize.X);
            if (ImGui.Combo("##feeds_combo", ref selected, itemsArray, itemsArray.Length)) {
                this.cursor = null;
                feed.Posts.Clear();
                this.selectedFeed = selected == 0 ? null : this.feeds[selected - 1];
                this.FetchPosts();
            }
            if (disabled) ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button(postText, postSize)) {
                var write = gui.SetView<WriteView>();
                write.Parent = this;
            }

            ImGui.SameLine();
            if (ImGui.Button(refreshText, refreshSize)) {
                this.cursor = null;
                feed.Posts.Clear();
                this.FetchPosts();
            }
        }, goBack: false);

        Components.Posts(feed.Posts.ToList(), this.FetchPosts);
    }

    public override void OnActivate() {
        this.cursor = null;
        this.bottom = false;
        feed.Reset();
        Task.Run(async () => {
            try {
                this.feeds = await feed.FetchFeeds();
            } catch (Exception e) {
                logger.LogError(e, "Failed to get feeds");
            }
        });
        this.FetchPosts();
    }

    private void FetchPosts() {
        if (this.fetchingTask?.IsCompleted == false) return;
        if (this.bottom) return;

        this.fetchingTask = Task.Run(async () => {
            logger.LogDebug("Fetching posts, cursor: {Cursor}", this.cursor);

            try {
                this.cursor = await feed.FetchFeed(this.selectedFeed, this.cursor);
                logger.LogDebug("Fetched posts, got cursor: {Cursor}", this.cursor);
                this.fetchingTask = null;
                this.bottom = this.cursor is null;
            } catch (Exception e) {
                logger.LogError(e, "Failed to get timeline");
            }

            if (!this.fetchedFeeds) {
                this.fetchedFeeds = true;

                try {
                    await feed.FetchFeeds();
                } catch (Exception e) {
                    logger.LogError(e, "Failed to get feeds");
                }
            }

            // sleep for a bit as a cooldown
            await Task.Delay(1000);
        });
    }
}
