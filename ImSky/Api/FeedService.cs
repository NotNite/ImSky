using FishyFlip.Models;
using FishyFlip.Tools;
using ImSky.Models;

namespace ImSky.Api;

public class FeedService(AtProtoService atProto) {
    public const int PageSize = 20;

    public List<Models.Post> Posts = new();

    public async Task<List<Feed>> FetchFeeds() {
        var preferences = (await atProto.AtProtocol.Actor.GetPreferencesAsync()).HandleResult();
        if (preferences is null) return [];

        var ids = new List<ATUri>();
        foreach (var pref in preferences.Preferences) {
            if (pref is SavedFeedsPref {Saved: not null} savedFeeds) ids.AddRange(savedFeeds.Saved);
        }

        var feeds = (await atProto.AtProtocol.Feed.GetFeedGeneratorsAsync(ids)).HandleResult();
        return feeds is null ? [] : feeds.Feeds.Select(f => new Feed(f)).ToList();
    }

    public void Reset() {
        this.Posts.Clear();
    }

    public async Task<string?> FetchUser(User user, bool likes = false, string? cursor = null) {
        var identifier = ATIdentifier.Create(user.Handle)!;
        var userFeed = (
                           likes
                               ? await atProto.AtProtocol.Feed.GetActorLikesAsync(identifier, PageSize, cursor)
                               : await atProto.AtProtocol.Feed.GetAuthorFeedAsync(identifier, PageSize, cursor)
                       ).HandleResult();
        this.ProcessFeedView(userFeed.Feed);
        return userFeed.Cursor;
    }

    public async Task<string?> FetchFeed(Feed? feed, string? cursor = null) {
        if (feed is null) {
            var timeline = (await atProto.AtProtocol.Feed.GetTimelineAsync(PageSize, cursor)).HandleResult();
            this.ProcessFeedView(timeline.Feed);
            return timeline.Cursor;
        } else {
            var timeline = (await atProto.AtProtocol.Feed.GetFeedAsync(feed.Uri, PageSize, cursor)).HandleResult();
            this.ProcessFeedView(timeline.Feed);
            return timeline.Cursor;
        }
    }

    private void ProcessFeedView(FeedViewPost[] feedView) {
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
            this.Posts.RemoveAll(p => p.PostId.ToString() == post.ReplyRoot?.PostId?.ToString());
            this.Posts.RemoveAll(p => p.PostId.ToString() == post.ReplyParent?.PostId?.ToString());
        }

        // Remove duplicate posts
        this.Posts = this.Posts.DistinctBy(p => p.PostId).ToList();
    }

    public async Task FetchReplies(Models.Post post) {
        var result = (await atProto.AtProtocol.Feed.GetPostThreadAsync(post.PostUri)).HandleResult();

        void ProcessReplies(Models.Post subpost, ThreadView[] replies) {
            foreach (var reply in replies) {
                if (reply.Post is null) continue;
                var replyPost = new Models.Post(reply.Post);
                if (subpost.Replies.Any(p => p.PostId.ToString() == replyPost.PostId.ToString())) continue;

                // Link the reply to the parent post
                replyPost.ReplyParent = subpost;
                replyPost.ReplyRoot = subpost.ReplyRoot ?? post.ReplyRoot ?? post;

                if (reply.Replies is not null) ProcessReplies(replyPost, reply.Replies);
                subpost.Replies.Add(replyPost);
            }
        }

        if (result.Thread.Replies is not null) ProcessReplies(post, result.Thread.Replies);
    }
}
