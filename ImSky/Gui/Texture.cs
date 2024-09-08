using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Veldrid;

namespace ImSky;

public class Texture : IDisposable {
    public double LastUsed = ImGui.GetTime();
    public nint? Handle = null;
    public Vector2? Size = null;

    public (byte[], uint, uint)? CreationData;

    public TextureView? View;
    public ResourceSet? Set;
    public nint? Global;

    public void Draw(Vector2? drawSize = null) {
        this.LastUsed = ImGui.GetTime();
        var actualSize = drawSize ?? this.Size ?? Vector2.Zero;
        if (this.Handle is null) {
            ImGui.Dummy(actualSize);
        } else {
            ImGui.Image(this.Handle.Value, actualSize);
        }
    }

    public void Dispose() {
        this.View?.Dispose();
        this.Set?.Dispose();
        if (this.Global != null) Marshal.FreeHGlobal(this.Global.Value);
    }
}
