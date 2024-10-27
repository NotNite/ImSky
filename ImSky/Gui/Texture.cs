using System.Numerics;
using Hexa.NET.ImGui;

namespace ImSky;

public class Texture : IDisposable {
    public double LastUsed = ImGui.GetTime();
    public nint? Handle;
    public Vector2? Size = null;

    public (byte[], uint, uint)? CreationData;

    public void Draw(Vector2? drawSize = null) {
        this.LastUsed = ImGui.GetTime();
        var actualSize = drawSize ?? this.Size ?? Vector2.Zero;
        if (this.Handle is null) {
            ImGui.Dummy(actualSize);
        } else {
            ImGui.Image((ulong) this.Handle, actualSize);
        }
    }

    public void Dispose() {
        this.Handle = null;
    }
}
