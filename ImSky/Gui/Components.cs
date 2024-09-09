using System.Numerics;
using ImGuiNET;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.Logging;
using ImageEmbed = ImSky.Models.ImageEmbed;
using Post = ImSky.Models.Post;

namespace ImSky;

public class Components {
    public static void Post(
        Post post,
        GuiService gui,
        bool inChild = false,
        bool skipContent = false,
        string? label = null
    ) {
        var y = ImGui.GetCursorPosY();

        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var spacing = style.ItemSpacing.Y;
        var size = (lineHeight * 2) + spacing;
        var scrollbar = ImGui.GetStyle().ScrollbarSize;
        var width = ImGui.GetContentRegionAvail().X - scrollbar;

        ImGui.BeginGroup();

        if (post.RepostedBy is { } repostedBy) {
            label = repostedBy.DisplayName is null
                        ? $"Reposted by @{post.RepostedBy.Handle}"
                        : $"Reposted by {repostedBy.DisplayName} (@{post.RepostedBy.Handle})";
        }

        if (!skipContent) {
            if (label is not null) {
                ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
                ImGui.SetNextItemWidth(width);
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
            posDate.X -= scrollbar;
            ImGui.SetCursorPos(posDate);
            ImGui.TextUnformatted(dateStr);

            var posNext = posPrev + new Vector2(0, lineHeight + spacing);
            ImGui.SetCursorPos(posNext);

            ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
            ImGui.TextUnformatted("@" + post.Author.Handle);
            ImGui.PopStyleColor();

            if (post.Text is not null) {
                ImGui.SetNextItemWidth(width);
                ImGui.TextWrapped(post.Text
                    // bad imgui escapes
                    .Replace("%", "%%")
                    // smart quotes
                    .Replace("\u201c", "\"")
                    .Replace("\u201d", "\"")
                    .Replace("\u2018", "'")
                );
            }
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

                    if (!skipContent) {
                        image.Draw(attachmentSize);
                        if (twoOrMore && i % 2 == 0 && i + 1 < post.Embeds.Count) {
                            ImGui.SameLine();
                        }
                    }
                    break;
                }

                case PostEmbed postEmbed: {
                    if (inChild) break;

                    const ImGuiChildFlags childFlags = ImGuiChildFlags.Border;
                    var childSize = postEmbed.Post.UiState.ContentHeight is { } contentHeight
                                        ? cra with {Y = contentHeight + (style.WindowPadding.Y * 2)}
                                        : Vector2.Zero;

                    ImGui.BeginChild($"##postchild_{post.PostUri}_{postEmbed.Post.PostUri}",
                        childSize, childFlags);
                    var childY = ImGui.GetCursorPosY();

                    Post(postEmbed.Post, gui, inChild: true, skipContent: skipContent);

                    var childNewY = ImGui.GetCursorPosY();
                    var childNewHeight = childNewY - childY;
                    if (!skipContent) postEmbed.Post.UiState.ContentHeight = childNewHeight;

                    ImGui.EndChild();
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
        if (!skipContent) post.UiState.ContentHeight = newHeight;
    }

    public static void PostInteraction(Post post, InteractionService interaction, ILogger logger) {
        // TODO: add replies
        if (Util.DisabledButton($"Like ({post.LikeCount})###like_{post.PostId}",
                post.UiState.LikeTask is not null || post.UiState.Liked)) {
            post.UiState.LikeTask = Task.Run(async () => {
                try {
                    await interaction.Like(post);
                } catch (Exception e) {
                    logger.LogError(e, "Failed to like post");
                } finally {
                    post.UiState.LikeTask = null;
                }
            });
        }

        ImGui.SameLine();

        // TODO: quote post
        if (Util.DisabledButton($"Repost ({post.RepostCount})###repost_{post.PostId}",
                post.UiState.RepostTask is not null || post.UiState.Reposted)) {
            post.UiState.RepostTask = Task.Run(async () => {
                try {
                    await interaction.Repost(post);
                } catch (Exception e) {
                    logger.LogError(e, "Failed to repost post");
                } finally {
                    post.UiState.RepostTask = null;
                }
            });
        }
    }

    public static bool MenuBar(Action draw, bool goBack = true) {
        var ret = false;
        if (goBack) {
            ret = ImGui.Button("<");
            ImGui.SameLine();
        }
        draw();
        ImGui.Separator();
        return ret;
    }

    public static void Replies(Post post, GuiService gui, InteractionService interaction, ILogger logger) {
        foreach (var reply in post.Replies) {
            IndentedPost(reply, () => {
                Post(reply, gui);
                PostInteraction(reply, interaction, logger);
                Replies(reply, gui, interaction, logger);
            });
        }
    }

    public static void IndentedPost(Post post, Action action) {
        const int indent = 20;

        ImGui.Dummy(new Vector2(indent, 0));
        ImGui.SameLine();

        var size = post.UiState.IndentHeight is { } indentHeight
                       ? new Vector2(0, indentHeight)
                       : Vector2.Zero;
        ImGui.BeginChild("##indent_" + post.PostUri, size, ImGuiChildFlags.AutoResizeY);
        var y = ImGui.GetCursorPosY();

        action();

        var newY = ImGui.GetCursorPosY();
        var newHeight = newY - y;
        post.UiState.IndentHeight = newHeight;

        ImGui.EndChild();
    }
}
