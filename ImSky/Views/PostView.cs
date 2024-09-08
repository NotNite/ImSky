using ImGuiNET;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class PostView(
    GuiService gui,
    FeedService feed,
    ILogger<PostView> logger
) : View {
    private readonly List<Post> stack = new();
    public Post? CurrentPost => this.stack.Count > 0 ? this.stack[^1] : null;
    private Task? fetchingTask;

    public void SetPost(Post post) {
        this.stack.Add(post);
    }

    public override void OnActivate() {
        if (this.CurrentPost is null) {
            gui.SetView<FeedsView>();
            return;
        }

        this.fetchingTask = Task.Run(async () => {
            try {
                await feed.FetchReplies(this.CurrentPost);
                logger.LogDebug("Fetched replies for post {PostId}", this.CurrentPost.PostId);
                this.fetchingTask = null;
            } catch (Exception e) {
                logger.LogError(e, "Failed to get replies for post {PostId}", this.CurrentPost.PostId);
            }
        });
    }

    public override void Draw() {
        if (this.CurrentPost is null) {
            gui.SetView<FeedsView>();
            return;
        }
        if (Components.MenuBar("Post")) {
            this.stack.RemoveAt(this.stack.Count - 1);
            return;
        }

        if (this.CurrentPost.ReplyRoot is not null) {
            Components.IndentedPost(this.CurrentPost.ReplyRoot, () => {
                Components.Post(this.CurrentPost.ReplyRoot, gui);
                Components.PostInteraction(this.CurrentPost.ReplyRoot, feed, logger);
            });
        }

        if (this.CurrentPost.ReplyParent is not null && this.CurrentPost.ReplyParent.PostId != this.CurrentPost.ReplyRoot?.PostId) {
            Components.IndentedPost(this.CurrentPost.ReplyParent, () => {
                Components.Post(this.CurrentPost.ReplyParent, gui);
                Components.PostInteraction(this.CurrentPost.ReplyParent, feed, logger);
            });
        }

        Components.Post(this.CurrentPost, gui);
        Components.PostInteraction(this.CurrentPost, feed, logger);

        ImGui.Separator();

        if (this.fetchingTask?.IsCompleted == false) {
            ImGui.Text("Loading...");
            return;
        }

        Components.Replies(this.CurrentPost, feed, gui, logger);
    }
}
