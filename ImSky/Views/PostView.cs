using Hexa.NET.ImGui;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;

namespace ImSky.Views;

public class PostView(
    GuiService gui,
    FeedService feed,
    ILogger<PostView> logger
) : View {
    public View? Parent;
    public Post? CurrentPost;
    private Task? fetchingTask;

    public void SetPost(Post post) {
        this.CurrentPost = post;
    }

    private void Retreat() {
        if (this.Parent is not null) {
            gui.SetView(this.Parent);
        } else {
            gui.SetView<FeedsView>();
        }
    }

    public override void OnActivate() {
        if (this.CurrentPost is null) {
            this.Retreat();
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
            this.Retreat();
            return;
        }

        Components.Hamburger();
        ImGui.SameLine();
        if (Components.MenuBar(() => ImGui.TextUnformatted("Post"))) {
            this.Retreat();
            return;
        }

        if (this.CurrentPost.ReplyRoot is not null) {
            ImGui.PushID(this.CurrentPost.ReplyRoot.PostId);
            Components.IndentedPost(this.CurrentPost.ReplyRoot, () => {
                Components.Post(this.CurrentPost.ReplyRoot);
                Components.PostInteraction(this.CurrentPost.ReplyRoot);
            });
            ImGui.PopID();
        }

        if (this.CurrentPost.ReplyParent is not null &&
            this.CurrentPost.ReplyParent.PostId != this.CurrentPost.ReplyRoot?.PostId) {
            ImGui.PushID(this.CurrentPost.ReplyParent.PostId);
            Components.IndentedPost(this.CurrentPost.ReplyParent, () => {
                Components.Post(this.CurrentPost.ReplyParent);
                Components.PostInteraction(this.CurrentPost.ReplyParent);
            });
            ImGui.PopID();
        }

        Components.Post(this.CurrentPost);
        Components.PostInteraction(this.CurrentPost);

        ImGui.Separator();

        if (this.fetchingTask?.IsCompleted == false) {
            ImGui.Text("Loading...");
            return;
        }

        Components.Replies(this.CurrentPost);
    }
}
