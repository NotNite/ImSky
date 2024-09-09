using FishyFlip.Models;
using Post = ImSky.Models.Post;

namespace ImSky.Api;

public class InteractionService(AtProtoService atProto) {
    public async Task Post(string content, Post? replyTo = null) {
        var replyRef = replyTo is not null ? new ReplyRef(replyTo.PostId, replyTo.PostUri) : null;
        var reply = replyRef is not null ? new Reply(replyRef, replyRef) : null;
        await atProto.AtProtocol.Repo.CreatePostAsync(content, reply);
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
