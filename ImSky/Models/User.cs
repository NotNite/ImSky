using System.Diagnostics.CodeAnalysis;
using FishyFlip.Models;

namespace ImSky.Models;

public record User {
    public required string Handle;
    public required string AvatarUrl;
    public readonly string? DisplayName;
    public readonly string? Banner;
    public readonly string? Description;

    public int FollowersCount;
    public int FollowingCount;
    public int PostCount;

    [SetsRequiredMembers]
    public User(FeedProfile feedProfile) {
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        this.Handle = feedProfile.Handle ?? "unknown";
        this.AvatarUrl = feedProfile.Avatar ?? string.Empty;
        this.DisplayName = feedProfile.DisplayName;
        this.Banner = feedProfile.Banner;
        this.FollowersCount = feedProfile.FollowersCount;
        this.FollowingCount = feedProfile.FollowsCount;
        this.PostCount = feedProfile.PostsCount;
        this.Description = feedProfile.Description;
    }

    [SetsRequiredMembers]
    public User(ActorProfile actorProfile) {
        this.Handle = actorProfile.Handle ?? "unknown";
        this.AvatarUrl = actorProfile.Avatar ?? string.Empty;
        this.DisplayName = actorProfile.DisplayName;
    }

    [SetsRequiredMembers]
    public User(FeedCreator actorProfile) {
        this.Handle = actorProfile.Handle;
        this.AvatarUrl = actorProfile.Avatar;
        this.DisplayName = actorProfile.DisplayName;
    }
}
