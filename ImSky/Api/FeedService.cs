using FishyFlip.Models;
using FishyFlip.Tools;
using ImSky.Models;

namespace ImSky.Api;

public class FeedService(AtProtoService atProto) {
    public const int PageSize = 20;

    public Feed? Feed;
    public List<Feed> Feeds = new();
    public List<Models.Post> Posts = new();

    public async Task FetchFeeds() {
        var preferences = (await atProto.AtProtocol.Actor.GetPreferencesAsync()).HandleResult();
        if (preferences is null) return;

        var ids = new List<ATUri>();
        foreach (var pref in preferences.Preferences) {
            if (pref is SavedFeedsPref {Saved: not null} savedFeeds) ids.AddRange(savedFeeds.Saved);
        }

        var feeds = (await atProto.AtProtocol.Feed.GetFeedGeneratorsAsync(ids)).HandleResult();
        if (feeds is null) return;

        this.Feeds.Clear();
        foreach (var feed in feeds.Feeds) {
            this.Feeds.Add(new Feed(feed));
        }
    }

    public async Task<string?> FetchPosts(string? cursor = null) {
        FeedViewPost[] feedView;
        string? newCursor;
        if (this.Feed is null) {
            var timeline = (await atProto.AtProtocol.Feed.GetTimelineAsync(PageSize, cursor)).HandleResult();
            feedView = timeline.Feed;
            newCursor = timeline.Cursor;
        } else {
            var feed = (await atProto.AtProtocol.Feed.GetFeedAsync(this.Feed.Uri, PageSize, cursor)).HandleResult();
            feedView = feed.Feed;
            newCursor = feed.Cursor;
        }

        foreach (var item in feedView) {
            this.Posts.Add(new Models.Post(item));
        }

        // If a post was reposted, filter the original entry, so the IDs don't conflict
        foreach (var post in this.Posts.ToList()) {
            foreach (var embed in post.Embeds) {
                if (embed is PostEmbed postEmbed) {
                    this.Posts.RemoveAll(p => p.PostId.ToString() == postEmbed.Post.PostId.ToString());
                }
            }

            // Same with replies - remove the original post(s)
            this.Posts.RemoveAll(p => p.PostId.ToString() == post.ReplyRoot?.PostId.ToString());
            this.Posts.RemoveAll(p => p.PostId.ToString() == post.ReplyParent?.PostId.ToString());
        }

        // Remove duplicate posts
        this.Posts = this.Posts.DistinctBy(p => p.PostId).ToList();

        return newCursor;
    }

    public async Task FetchReplies(Models.Post post) {
        var result = (await atProto.AtProtocol.Feed.GetPostThreadAsync(post.PostUri)).HandleResult();

        void Process(Models.Post subpost, ThreadView[] replies) {
            foreach (var reply in replies) {
                if (reply.Post is null) continue;
                var replyPost = new Models.Post(reply.Post);
                if (subpost.Replies.Any(p => p.PostId.ToString() == replyPost.PostId.ToString())) continue;

                // Link the reply to the parent post
                replyPost.ReplyParent = subpost;
                replyPost.ReplyRoot = subpost.ReplyRoot ?? post;

                if (reply.Replies is not null) Process(replyPost, reply.Replies);
                subpost.Replies.Add(replyPost);
            }
        }

        if (result.Thread.Replies is not null) Process(post, result.Thread.Replies);
    }
}
