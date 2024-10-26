using FishyFlip.Models;
using FishyFlip.Tools;
using Serilog;
using Post = ImSky.Models.Post;

namespace ImSky.Api;

public class InteractionService(AtProtoService atProto) {
    public async Task<Post> Post(string content, Post? replyTo = null) {
        var replyRef = replyTo is not null ? new ReplyRef(replyTo.PostId, replyTo.PostUri) : null;
        var rootRef = replyTo?.ReplyRoot is not null
                          ? new ReplyRef(replyTo.ReplyRoot.PostId, replyTo.ReplyRoot.PostUri)
                          : replyRef;
        var reply = replyRef is not null ? new Reply(rootRef!, replyRef) : null;

        /*Log.Debug("replyTo.PostId: {PostId}", replyTo?.PostId);
        Log.Debug("replyTo.ReplyRoot.PostId: {PostId}", replyTo?.ReplyRoot?.PostId);
        Log.Debug("Reply ref: {ReplyRef}", replyRef);
        Log.Debug("Root ref: {RootRef}", rootRef);
        Log.Debug("Reply: {Reply}", reply);*/

        var postData = (await atProto.AtProtocol.Repo.CreatePostAsync(content, reply)).HandleResult();
        if (postData.Uri is null) throw new Exception("Failed to create post");

        var fullPost = (await atProto.AtProtocol.Feed.GetPostThreadAsync(postData.Uri)).HandleResult();
        if (fullPost.Thread.Post is null) throw new Exception("Failed to get post");

        var post = new Post(fullPost.Thread.Post) {
            ReplyParent = replyTo,
            ReplyRoot = replyTo?.ReplyRoot ?? replyTo
        };
        return post;
    }

    public async Task Like(Post post) {
        await atProto.AtProtocol.Repo.CreateLikeAsync(post.PostId, post.PostUri);
        post.LikeCount++;
        post.UiState.Liked = true;
    }

    public async Task Repost(Post post) {
        await atProto.AtProtocol.Repo.CreateRepostAsync(post.PostId, post.PostUri);
        post.RepostCount++;
        post.UiState.Reposted = true;
    }
}
