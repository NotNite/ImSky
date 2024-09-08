namespace ImSky.Models;

public record Embed;

public record ImageEmbed : Embed {
    public required string ThumbnailUrl;
    public string? FullUrl;
}

public record PostEmbed : Embed {
    public required Post Post;
}
