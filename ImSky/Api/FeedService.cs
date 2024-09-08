using FishyFlip.Models;
using FishyFlip.Tools;
using ImSky.Models;

namespace ImSky.Api;

public class FeedService(AtProtoService atProto) {
    public const int PageSize = 20;

    public ATUri? Feed;
    public List<Models.Post> Posts = new();

    public async Task<string?> FetchPosts(string? cursor = null) {
        FeedViewPost[] feedView;
        string? newCursor;
        if (this.Feed is null) {
            var timeline = (await atProto.AtProtocol.Feed.GetTimelineAsync(PageSize, cursor)).HandleResult();
            feedView = timeline.Feed;
            newCursor = timeline.Cursor;
        } else {
            var feed = (await atProto.AtProtocol.Feed.GetFeedAsync(this.Feed, PageSize, cursor)).HandleResult();
            feedView = feed.Feed;
            newCursor = feed.Cursor;
        }

        foreach (var item in feedView) {
            this.Posts.Add(new Models.Post(item));
        }

        // If a post was reposted, filter the original entry
        foreach (var post in this.Posts.ToList()) {
            foreach (var embed in post.Embeds) {
                if (embed is PostEmbed postEmbed) {
                    this.Posts.RemoveAll(p => p.PostId.ToString() == postEmbed.Post.PostId.ToString());
                }
            }
        }

        // Remove duplicate posts
        this.Posts = this.Posts.DistinctBy(p => p.PostId).ToList();

        return newCursor;
    }

    public async Task Like(Models.Post post) {
        await atProto.AtProtocol.Repo.CreateLikeAsync(post.PostId, post.PostUri);
        post.LikeCount++;
        post.UiState.Liked = true;
    }

    public async Task Repost(Models.Post post) {
        await atProto.AtProtocol.Repo.CreateRepostAsync(post.PostId, post.PostUri);
        post.RepostCount++;
        post.UiState.Reposted = true;
    }
}
