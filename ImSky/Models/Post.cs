using System.Diagnostics.CodeAnalysis;
using FishyFlip.Models;

namespace ImSky.Models;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
public record Post {
    public readonly Ipfs.Cid PostId;
    public readonly ATUri PostUri;

    public readonly User? Author;
    public readonly string? Text;
    public required List<Embed> Embeds;
    public DateTime CreatedAt;

    public Post? ReplyParent;
    public Post? ReplyRoot;
    public readonly User? RepostedBy;
    public readonly List<Post> Replies = [];

    public int LikeCount;
    public int RepostCount;
    public int ReplyCount;

    public readonly PostUiState UiState = new();

    [SetsRequiredMembers]
    public Post(PostView post) {
        this.PostId = post.Cid;
        this.PostUri = post.Uri;

        this.Author = post.Author is not null ? new User(post.Author) : null;
        this.Text = post.Record?.Text;
        this.CreatedAt = post.Record?.CreatedAt ?? DateTime.Now;

        this.Embeds = [];
        switch (post.Embed) {
            case ImageViewEmbed imageView: {
                foreach (var image in imageView.Images) {
                    this.Embeds.Add(new ImageEmbed {
                        ThumbnailUrl = image.Thumb,
                        FullUrl = image.Fullsize
                    });
                }
                break;
            }

            case RecordViewEmbed recordView: {
                this.Embeds.Add(new PostEmbed {
                    Post = new Post(recordView.Post)
                });
                break;
            }

            case RecordWithMediaViewEmbed recordWithMediaView: {
                if (recordWithMediaView.Record is null) break;
                this.Embeds.Add(new PostEmbed {
                    Post = new Post(recordWithMediaView.Record.Post)
                });
                break;
            }
        }

        this.LikeCount = post.LikeCount;
        this.RepostCount = post.RepostCount;
        this.ReplyCount = post.ReplyCount;
    }

    [SetsRequiredMembers]
    public Post(FeedViewPost feedView) : this(feedView.Post) {
        if (feedView.Reply?.Parent is not null) this.ReplyParent = new Post(feedView.Reply.Parent);
        if (feedView.Reply?.Root is not null) this.ReplyRoot = new Post(feedView.Reply.Root);
        if (feedView.Reason is {Type: "app.bsky.feed.defs#reasonRepost", By: not null}) {
            this.RepostedBy = new User(feedView.Reason.By);
        }
    }
}

public record PostUiState {
    public float? ContentHeight;
    public float? IndentHeight;
    public float? TotalHeight;

    public bool Liked;
    public bool Reposted;

    public Task? LikeTask;
    public Task? RepostTask;
    public Task? ReplyTask;
    public Task? OpenTask;

    public bool IsReplying;
    public string ReplyText = string.Empty;
}
