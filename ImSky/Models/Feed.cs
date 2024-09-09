using System.Diagnostics.CodeAnalysis;
using FishyFlip.Models;

namespace ImSky.Models;

public record Feed {
    public required ATUri Uri;
    public required string Title;
    public required User Author;

    [SetsRequiredMembers]
    public Feed(FeedRecord feed) {
        this.Uri = feed.Uri;
        this.Title = feed.DisplayName;
        this.Author = new User(feed.Creator);
    }
}
