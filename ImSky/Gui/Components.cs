using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using ImGuiNET;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;
using ImageEmbed = ImSky.Models.ImageEmbed;
using Post = ImSky.Models.Post;

namespace ImSky;

public class Components {
    public static void Post(Post post, GuiService gui, bool inChild = false, string? label = null) {
        var y = ImGui.GetCursorPosY();

        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var spacing = style.ItemSpacing.Y;
        var size = (lineHeight * 2) + spacing;

        ImGui.BeginGroup();

        if (post.RepostedBy is { } repostedBy) {
            label = repostedBy.DisplayName is null
                        ? $"Reposted by @{post.RepostedBy.Handle}"
                        : $"Reposted by {repostedBy.DisplayName} (@{post.RepostedBy.Handle})";
        }

        if (label is not null) {
            ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();
        }

        var avatar = gui.GetTexture(post.Author.AvatarUrl);
        avatar.Draw(new Vector2(size, size));
        ImGui.SameLine();

        var posPrev = ImGui.GetCursorPos();
        ImGui.TextUnformatted(post.Author.DisplayName ?? "@" + post.Author.Handle);

        var dateStr = Util.FormatRelative(DateTime.UtcNow - post.CreatedAt);
        var dateStrSize = ImGui.CalcTextSize(dateStr);
        var posDate = posPrev with {X = ImGui.GetWindowWidth() - dateStrSize.X - ImGui.GetStyle().WindowPadding.X};
        ImGui.SetCursorPos(posDate);
        ImGui.TextUnformatted(dateStr);

        var posNext = posPrev + new Vector2(0, lineHeight + spacing);
        ImGui.SetCursorPos(posNext);

        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
        ImGui.TextUnformatted("@" + post.Author.Handle);
        ImGui.PopStyleColor();

        if (post.Text is not null) {
            ImGui.TextWrapped(post.Text
                // bad imgui escapes
                .Replace("%", "%%")
                // smart quotes
                .Replace("\u201c", "\"")
                .Replace("\u201d", "\"")
                .Replace("\u2018", "'")
            );
        }

        var twoOrMore = post.Embeds.Count > 1;
        var cra = ImGui.GetContentRegionAvail();
        foreach (var (embed, i) in post.Embeds.Select((x, i) => (x, i))) {
            switch (embed) {
                case ImageEmbed imageEmbed: {
                    var image = gui.GetTexture(imageEmbed.ThumbnailUrl);
                    var maxWidth = cra.X - 100;
                    var maxHeight = lineHeight * 20;

                    if (twoOrMore) maxWidth /= 2;

                    var attachmentSize = image.Size ?? Vector2.One;
                    var ratio = attachmentSize.X / attachmentSize.Y;

                    if (attachmentSize.X > maxWidth) {
                        attachmentSize = new Vector2(maxWidth, maxWidth / ratio);
                    }

                    if (attachmentSize.Y > maxHeight) {
                        attachmentSize = new Vector2(maxHeight * ratio, maxHeight);
                    }

                    image.Draw(attachmentSize);

                    if (twoOrMore && i % 2 == 0 && i + 1 < post.Embeds.Count) {
                        ImGui.SameLine();
                    }
                    break;
                }

                case PostEmbed postEmbed: {
                    if (inChild) break;

                    const ImGuiChildFlags childFlags = ImGuiChildFlags.Border;
                    var childSize = postEmbed.Post.UiState.ContentHeight is { } contentHeight
                                        ? cra with {Y = contentHeight + (style.WindowPadding.Y * 2)}
                                        : Vector2.Zero;
                    if (ImGui.BeginChild($"postchild_{post.PostId}_{postEmbed.Post.PostId}", childSize, childFlags)) {
                        var childY = ImGui.GetCursorPosY();

                        Post(postEmbed.Post, gui, inChild: true);

                        var childNewY = ImGui.GetCursorPosY();
                        var childNewHeight = childNewY - childY;
                        postEmbed.Post.UiState.ContentHeight = childNewHeight;

                        ImGui.EndChild();
                    }

                    break;
                }
            }
        }

        ImGui.EndGroup();
        if (ImGui.IsItemClicked()) {
            var view = gui.SetView<Views.PostView>();
            view.SetPost(post);
        }

        var newY = ImGui.GetCursorPosY();
        var newHeight = newY - y;
        post.UiState.ContentHeight = newHeight;
    }

    public static void PostInteraction(Post post, FeedService feed, ILogger logger) {
        // TODO: add replies
        if (Util.DisabledButton($"Like ({post.LikeCount})###like_{post.PostId}",
                post.UiState.LikeTask is not null || post.UiState.Liked)) {
            post.UiState.LikeTask = Task.Run(async () => {
                try {
                    await feed.Like(post);
                } catch (Exception e) {
                    logger.LogError(e, "Failed to like post");
                } finally {
                    post.UiState.LikeTask = null;
                }
            });
        }

        ImGui.SameLine();

        if (Util.DisabledButton($"Repost ({post.RepostCount})###repost_{post.PostId}",
                post.UiState.RepostTask is not null || post.UiState.Reposted)) {
            post.UiState.RepostTask = Task.Run(async () => {
                try {
                    await feed.Repost(post);
                } catch (Exception e) {
                    logger.LogError(e, "Failed to repost post");
                } finally {
                    post.UiState.RepostTask = null;
                }
            });
        }
    }

    public static bool MenuBar(string label) {
        var ret = ImGui.Button("<");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.Separator();
        return ret;
    }

    public static void Replies(Post post, FeedService feed, GuiService gui, ILogger logger) {
        foreach (var reply in post.Replies) {
            IndentedPost(reply, () => {
                Post(reply, gui);
                PostInteraction(reply, feed, logger);
                Replies(reply, feed, gui, logger);
            });
        }
    }

    public static void IndentedPost(Post post, Action action) {
        const int indent = 20;

        ImGui.Dummy(new Vector2(indent, 0));
        ImGui.SameLine();

        var size = post.UiState.ContentHeight is { } contentHeight
                       ? new Vector2(0, contentHeight)
                       : Vector2.Zero;
        if (ImGui.BeginChild("##indent_" + post.PostUri, size)) {
            var y = ImGui.GetCursorPosY();

            action();

            var newY = ImGui.GetCursorPosY();
            var newHeight = newY - y;
            post.UiState.ContentHeight = newHeight;

            ImGui.EndChild();
        }
    }
}
