﻿using System.Numerics;
using Hexa.NET.ImGui;
using ImSky.Api;
using ImSky.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ImageEmbed = ImSky.Models.ImageEmbed;
using Post = ImSky.Models.Post;

namespace ImSky;

public class Components {
    // This sucks lol
    private static Lazy<AtProtoService> AtProto =>
        new(() => Program.Host.Services.GetRequiredService<AtProtoService>());
    private static Lazy<FeedService> Feed =>
        new(() => Program.Host.Services.GetRequiredService<FeedService>());
    private static Lazy<InteractionService> Interaction =>
        new(() => Program.Host.Services.GetRequiredService<InteractionService>());
    private static Lazy<GuiService> Gui => new(() => Program.Host.Services.GetRequiredService<GuiService>());

    public static void Post(
        Post post,
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

        if (post.RepostedBy is { } repostedBy) {
            label = repostedBy.DisplayName is null
                        ? $"Reposted by @{post.RepostedBy.Handle}"
                        : $"Reposted by {repostedBy.DisplayName} (@{post.RepostedBy.Handle})";
        }

        if (!skipContent) {
            if (label is not null) {
                ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
                ImGui.SetNextItemWidth(width);
                ImGui.TextUnformatted(Util.StripWeirdCharacters(label, true));
                ImGui.PopStyleColor();
            }

            ImGui.BeginGroup();
            var avatarUrl = post.Author?.AvatarUrl;
            var avatarSize = new Vector2(size, size);
            if (avatarUrl is not null) {
                var avatar = Gui.Value.GetTexture(avatarUrl);
                avatar.Draw(avatarSize);
            } else {
                ImGui.Dummy(avatarSize);
            }
            ImGui.SameLine();

            var posPrev = ImGui.GetCursorPos();
            ImGui.TextUnformatted(
                Util.StripWeirdCharacters(post.Author?.DisplayName ?? "@" + post.Author?.Handle, true));

            var dateStr = Util.FormatRelative(DateTime.UtcNow - post.CreatedAt);
            var dateStrSize = ImGui.CalcTextSize(dateStr);
            var posDate = posPrev with {X = ImGui.GetWindowWidth() - dateStrSize.X - ImGui.GetStyle().WindowPadding.X};
            posDate.X -= scrollbar;
            ImGui.SetCursorPos(posDate);
            ImGui.TextUnformatted(dateStr);

            var posNext = posPrev + new Vector2(0, lineHeight + spacing);
            ImGui.SetCursorPos(posNext);

            ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
            ImGui.TextUnformatted("@" + post.Author?.Handle);
            ImGui.PopStyleColor();
            ImGui.EndGroup();
            if (ImGui.IsItemClicked()) {
                var view = Gui.Value.SetView<Views.UserView>();
                view.Handle = post.Author?.Handle;
                view.Parent = Gui.Value.GetView();
            }

            if (post.Text is not null) {
                ImGui.SetNextItemWidth(width);
                var disabled = post.UiState.OpenTask is not null;
                if (disabled) ImGui.PushStyleColor(ImGuiCol.Text, Colors.Grey);
                ImGui.TextWrapped(Util.StripWeirdCharacters(post.Text));
                if (disabled) ImGui.PopStyleColor();
                if (ImGui.IsItemClicked() && post.UiState.OpenTask is null) {
                    post.UiState.OpenTask = Task.Run(async () => {
                        try {
                            try {
                                await Feed.Value.LookupReplyRef(post);
                            } catch (Exception e) {
                                Log.Error(e, "Failed to lookup reply ref");
                            }

                            var view = Gui.Value.SetView<Views.PostView>();
                            view.Parent = Gui.Value.GetView();
                            Log.Debug("Opening post {PostId}", post.PostId);
                            view.SetPost(post);
                        } catch (Exception e) {
                            Log.Error(e, "Failed to open post");
                        } finally {
                            post.UiState.OpenTask = null;
                        }
                    });
                }
            }
        }

        var twoOrMore = post.Embeds.Count > 1;
        var cra = ImGui.GetContentRegionAvail();
        foreach (var (embed, i) in post.Embeds.Select((x, i) => (x, i))) {
            switch (embed) {
                case ImageEmbed imageEmbed: {
                    var image = Gui.Value.GetTexture(imageEmbed.ThumbnailUrl);
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

                    const ImGuiChildFlags childFlags = ImGuiChildFlags.Borders;
                    var childSize = postEmbed.Post.UiState.ContentHeight is { } contentHeight
                                        ? cra with {Y = contentHeight + (style.WindowPadding.Y * 2)}
                                        : Vector2.Zero;

                    ImGui.BeginChild($"##postchild_{post.PostUri}_{postEmbed.Post.PostUri}",
                        childSize, childFlags);
                    var childY = ImGui.GetCursorPosY();

                    Post(postEmbed.Post, inChild: true, skipContent: skipContent);

                    var childNewY = ImGui.GetCursorPosY() + style.ItemSpacing.Y;
                    var childNewHeight = childNewY - childY;
                    if (!skipContent) postEmbed.Post.UiState.ContentHeight = childNewHeight;

                    ImGui.EndChild();
                    break;
                }
            }
        }

        var newY = ImGui.GetCursorPosY() + style.ItemSpacing.Y;
        var newHeight = newY - y;
        if (!skipContent) post.UiState.ContentHeight = newHeight;
    }

    public static void PostInteraction(Post post) {
        if (Util.DisabledButton($"Reply ({post.ReplyCount})###reply_{post.PostId}",
                post.UiState.ReplyTask is not null)) {
            post.UiState.ReplyTask = Task.Run(async () => {
                try {
                    try {
                        await Feed.Value.LookupReplyRef(post);
                    } catch (Exception e) {
                        Log.Error(e, "Failed to lookup reply ref");
                    }

                    var view = Gui.Value.SetView<Views.WriteView>();
                    view.Parent = Gui.Value.GetView();
                    view.ReplyTo = post;
                } catch (Exception e) {
                    Log.Error(e, "Failed to reply to post");
                } finally {
                    post.UiState.ReplyTask = null;
                }
            });
        }

        ImGui.SameLine();

        // TODO: quote post
        if (Util.DisabledButton($"Repost ({post.RepostCount})###repost_{post.PostId}",
                post.UiState.RepostTask is not null || post.UiState.Reposted)) {
            post.UiState.RepostTask = Task.Run(async () => {
                try {
                    await Interaction.Value.Repost(post);
                } catch (Exception e) {
                    Log.Error(e, "Failed to repost post");
                } finally {
                    post.UiState.RepostTask = null;
                }
            });
        }

        ImGui.SameLine();

        if (Util.DisabledButton($"Like ({post.LikeCount})###like_{post.PostId}",
                post.UiState.LikeTask is not null || post.UiState.Liked)) {
            post.UiState.LikeTask = Task.Run(async () => {
                try {
                    await Interaction.Value.Like(post);
                } catch (Exception e) {
                    Log.Error(e, "Failed to like post");
                } finally {
                    post.UiState.LikeTask = null;
                }
            });
        }
    }

    public static bool Hamburger() {
        var ret = false;

        if (ImGui.BeginPopup("##hamburger")) {
            if (ImGui.MenuItem("Feeds")) {
                Gui.Value.SetView<Views.FeedsView>();
                ret = true;
            }

            if (ImGui.MenuItem("Profile")) {
                if (AtProto.Value.AtProtocol.Session is not null) {
                    var userView = Gui.Value.SetView<Views.UserView>();
                    userView.Handle = AtProto.Value.AtProtocol.Session.Handle.Handle;
                    userView.Parent = Gui.Value.GetView();
                    ret = true;
                }
            }

            if (ImGui.MenuItem("Settings")) {
                Gui.Value.SetView<Views.SettingsView>();
                ret = true;
            }

            if (ImGui.MenuItem("Logout")) {
                AtProto.Value.LogOut();
                Gui.Value.SetView<Views.LoginView>();
                ret = true;
            }

            ImGui.EndPopup();
        }

        if (ImGui.Button("=")) {
            ImGui.OpenPopup("##hamburger");
        }

        return ret;
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

    public static void Posts(
        IEnumerable<Post> posts,
        Action fetchPosts
    ) {
        if (ImGui.BeginChild("##feeds", Vector2.Zero)) {
            var visibleStart = ImGui.GetScrollY();
            var visibleEnd = visibleStart + ImGui.GetWindowHeight();

            foreach (var post in posts) {
                var y = ImGui.GetCursorPosY();
                var totalHeight = post.UiState.TotalHeight;
                var offScreen = totalHeight is not null && (y + totalHeight < visibleStart || y > visibleEnd);

                if (post.ReplyRoot is not null) {
                    Post(post.ReplyRoot, skipContent: offScreen);
                    if (!offScreen) PostInteraction(post.ReplyRoot);
                }
                if (post.ReplyParent is not null && post.ReplyParent.PostId != post.ReplyRoot?.PostId) {
                    Post(post.ReplyParent, skipContent: offScreen);
                    if (!offScreen) PostInteraction(post.ReplyParent);
                }

                Post(post, skipContent: offScreen);
                if (!offScreen) PostInteraction(post);

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
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) fetchPosts();
        }
        ImGui.EndChild();
    }

    public static void Replies(Post post) {
        foreach (var reply in post.Replies) {
            IndentedPost(reply, () => {
                Post(reply);
                PostInteraction(reply);
                Replies(reply);
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
        var newHeight = newY - y + ImGui.GetStyle().ItemSpacing.Y;
        post.UiState.IndentHeight = newHeight;

        ImGui.EndChild();
    }
}
