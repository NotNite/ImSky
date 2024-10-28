using System.Numerics;
using Hexa.NET.ImGui;
using ImSky.Api;

namespace ImSky.Views;

public class WriteView(
    GuiService gui,
    InteractionService interaction,
    FeedService feed
) : View {
    public const int WriteLimit = 300;

    public View? Parent;
    public Models.Post? ReplyTo;
    public Models.Post? Quote;

    public int CharsLeft => WriteLimit - this.content.Length;
    public bool OverLimit => this.CharsLeft < 0;

    private string content = string.Empty;
    private Task? uploadTask;

    public override void OnActivate() {
        this.content = string.Empty;
    }

    private void Retreat() {
        if (this.Parent is not null) {
            gui.SetView(this.Parent);
        } else {
            gui.SetView<FeedsView>();
        }
    }

    public override void Draw() {
        Components.Hamburger();
        ImGui.SameLine();
        if (Components.MenuBar(() => ImGui.TextUnformatted("Write"))) {
            this.Retreat();
            return;
        }

        if (this.ReplyTo is not null) {
            Components.IndentedPost(this.ReplyTo, () => { Components.Post(this.ReplyTo); });
        }

        ImGui.InputTextMultiline("##write_content", ref this.content, 1024, Vector2.Zero);

        var disabled = this.uploadTask?.IsCompleted == false || this.OverLimit;
        if (disabled) ImGui.BeginDisabled();
        if (ImGui.Button("Post")) {
            this.uploadTask = Task.Run(async () => {
                var post = await interaction.Post(this.content, this.ReplyTo);
                feed.Posts.Insert(0, post);
                if (this.ReplyTo is not null) {
                    this.ReplyTo.Replies.Add(post);
                    this.ReplyTo.ReplyCount++;
                }
                this.Retreat();
            });
        }
        if (disabled) ImGui.EndDisabled();

        ImGui.SameLine();
        var chars = this.CharsLeft.ToString();
        if (this.OverLimit) {
            ImGui.TextColored(Colors.Red, chars);
        } else {
            ImGui.TextUnformatted(chars);
        }

        if (this.uploadTask?.IsFaulted == true) {
            ImGui.TextColored(Colors.Red, this.uploadTask.Exception?.ToString());
        }
    }
}
