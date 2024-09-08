using System.Diagnostics.CodeAnalysis;
using FishyFlip.Models;

namespace ImSky.Models;

public record User {
    public required string Handle;
    public required string AvatarUrl;
    public readonly string? DisplayName;

    [SetsRequiredMembers]
    public User(FeedProfile feedProfile) {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        this.Handle = feedProfile?.Handle ?? "unknown";
        this.AvatarUrl = feedProfile?.Avatar ?? string.Empty;
        this.DisplayName = feedProfile?.DisplayName;
    }

    [SetsRequiredMembers]
    public User(ActorProfile actorProfile) {
        this.Handle = actorProfile.Handle ?? "unknown";
        this.AvatarUrl = actorProfile.Avatar ?? string.Empty;
        this.DisplayName = actorProfile.DisplayName;
    }
}
