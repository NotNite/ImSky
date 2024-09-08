using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using ImSky.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Point = Veldrid.Point;

namespace ImSky;

public class GuiService(Config config, ILogger<GuiService> logger) : IHostedService {
    public const bool ShowImGuiDebug = false;
    private static readonly RgbaFloat BackgroundColor = new(0.1f, 0.1f, 0.1f, 1f);

    private Task task = null!;
    private CancellationTokenSource cts = null!;
    private readonly string iniPath = Path.Combine(Program.AppDir, "imgui.ini");

    private Sdl2Window window = null!;
    private GraphicsDevice gd = null!;
    private ImGuiRenderer imgui = null!;
    private CommandList cl = null!;
    private ResourceLayout textureLayout = null!;

    private readonly HttpClient client = new();
    private readonly Dictionary<string, Texture> textures = new();

    private View? currentView;
    private View? queuedView;
    private string windowName = string.Empty;

    public Task StartAsync(CancellationToken cancellationToken) {
        this.cts = new CancellationTokenSource();
        this.task = Task.Run(this.Run, this.cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        this.cts.Cancel();
        return this.task;
    }

    public T SetView<T>() where T : View {
        var view = Program.Host.Services.GetRequiredService<T>();
        this.queuedView = view;
        return view;
    }

    public void ProcessQueuedView() {
        if (this.queuedView != null) {
            this.currentView?.OnDeactivate();
            this.currentView = this.queuedView;
            this.queuedView = null;
            this.currentView.OnActivate();
            this.windowName = this.currentView.GetType().Name;
        }
    }

    public void Run() {
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(config.WindowX, config.WindowY, config.WindowWidth,
                config.WindowHeight,
                WindowState.Normal, "ImSky"),
            out this.window,
            out this.gd
        );

        this.textureLayout = this.gd.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(new ResourceLayoutElementDescription(
                "MainTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment
            )));

        this.imgui = new ImGuiRenderer(
            this.gd,
            this.gd.MainSwapchain.Framebuffer.OutputDescription,
            this.window.Width, this.window.Height
        );
        var io = ImGui.GetIO();
        unsafe {
            io.NativePtr->IniFilename = null;
            ImGui.LoadIniSettingsFromDisk(this.iniPath);
        }
        this.cl = this.gd.ResourceFactory.CreateCommandList();

        this.gd.SyncToVerticalBlank = true;

        this.window.Resized += this.Resized;
        this.window.Moved += this.Moved;

        var stopwatch = Stopwatch.StartNew();
        while (this.window.Exists && !this.cts.Token.IsCancellationRequested) {
            var deltaTime = stopwatch.ElapsedTicks / (float) Stopwatch.Frequency;
            stopwatch.Restart();

            var snapshot = this.window.PumpEvents();
            if (!this.window.Exists) break;

            lock (this.imgui) {
                this.ProcessTextures();
                this.imgui.Update(deltaTime, snapshot);
                try {
                    this.Draw();
                } catch (Exception e) {
                    logger.LogError(e, "Error in draw loop");
                }

                this.cl.Begin();
                this.cl.SetFramebuffer(this.gd.MainSwapchain.Framebuffer);
                this.cl.ClearColorTarget(0, BackgroundColor);

                this.imgui.Render(this.gd, this.cl);
                this.cl.End();
                this.gd.SubmitCommands(this.cl);
                this.gd.SwapBuffers(this.gd.MainSwapchain);
            }
        }

        this.gd.WaitForIdle();

        this.window.Resized -= this.Resized;
        this.window.Moved -= this.Moved;

        ImGui.SaveIniSettingsToDisk(this.iniPath);

        this.cl.Dispose();
        this.imgui.Dispose();
        this.textureLayout.Dispose();
        this.gd.Dispose();
        this.window.Close();

        Program.Host.StopAsync();
    }

    private void Moved(Point point) {
        config.WindowX = point.X;
        config.WindowY = point.Y;
        config.Save();
    }

    private void Resized() {
        this.gd.MainSwapchain.Resize((uint) this.window.Width, (uint) this.window.Height);
        this.imgui.WindowResized(this.window.Width, this.window.Height);
        config.WindowWidth = this.window.Width;
        config.WindowHeight = this.window.Height;
        config.Save();
    }

    private void Draw() {
        if (this.currentView == null) this.SetView<LoginView>();
        this.ProcessQueuedView();

        this.currentView!.PreDraw();

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize
                                       | ImGuiWindowFlags.NoCollapse
                                       | ImGuiWindowFlags.NoDecoration;

        const int debugWidth = 500;
        var size = ImGui.GetIO().DisplaySize;
        if (ShowImGuiDebug) size.X -= debugWidth;
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowPos(Vector2.Zero);
        if (ImGui.Begin(this.windowName, flags)) {
            try {
                this.currentView.Draw();
            } catch (Exception e) {
                logger.LogError(e, "Error in draw");
            }
        }
        ImGui.End();

        this.currentView.PostDraw();

        if (ShowImGuiDebug) {
            ImGui.SetNextWindowSize(size with {X = debugWidth}, ImGuiCond.Always);
            ImGui.SetNextWindowPos(size with {Y = 0}, ImGuiCond.Always);
            ImGui.ShowMetricsWindow();
        }
    }

    public Texture GetTexture(string url) {
        if (this.textures.TryGetValue(url, out var tex)) {
            tex.LastUsed = ImGui.GetTime();
            return tex;
        } else {
            var newTex = this.textures[url] = new Texture();
            Task.Run(async () => {
                if (string.IsNullOrWhiteSpace(url)) return;

                try {
                    var data = await this.client.GetByteArrayAsync(url);
                    logger.LogDebug("Loaded texture from {Url}", url);

                    var img = Image.Load(data);
                    var rgba = img.CloneAs<Bgra32>();

                    var bytes = new byte[rgba.Width * rgba.Height * 4];
                    rgba.CopyPixelDataTo(bytes);

                    newTex.CreationData = (bytes, (uint) rgba.Width, (uint) rgba.Height);
                } catch (Exception e) {
                    logger.LogError(e, "Failed to load texture");
                }
            });
            return newTex;
        }
    }

    private void ProcessTextures() {
        const int seconds = 5;
        var cutoff = ImGui.GetTime() < seconds ? 0 : ImGui.GetTime() - seconds;
        foreach (var (key, value) in this.textures) {
            if (value.LastUsed < cutoff) {
                this.textures.Remove(key);
                this.imgui.RemoveImGuiBinding(value.View!);
                value.Dispose();
            }
        }

        foreach (var texWrapper in this.textures.Values) {
            if (texWrapper.CreationData is not null) {
                this.CreateTextureFromRgba(texWrapper);
            }
        }
    }

    private void CreateTextureFromRgba(Texture texWrapper) {
        var (data, width, height) = texWrapper.CreationData ?? throw new InvalidOperationException("No creation data");

        var texture = this.gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            width,
            height,
            1,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled
        ));

        var global = Marshal.AllocHGlobal(data.Length);
        for (var i = 0; i < data.Length; i += 4) {
            var b = data[i];
            var g = data[i + 1];
            var r = data[i + 2];
            var a = data[i + 3];

            Marshal.WriteByte(global, i, r);
            Marshal.WriteByte(global, i + 1, g);
            Marshal.WriteByte(global, i + 2, b);
            Marshal.WriteByte(global, i + 3, a);
        }

        this.gd.UpdateTexture(
            texture,
            global,
            4 * width * height,
            0,
            0,
            0,
            width,
            height,
            1,
            0,
            0
        );

        var textureView = this.gd.ResourceFactory.CreateTextureView(texture);
        var resourceSet = this.gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
            this.textureLayout,
            textureView
        ));

        var binding = this.imgui.GetOrCreateImGuiBinding(this.gd.ResourceFactory, textureView);
        texWrapper.View = textureView;
        texWrapper.Set = resourceSet;
        texWrapper.Global = global;
        texWrapper.Size = new Vector2(width, height);
        texWrapper.Handle = binding;

        texWrapper.CreationData = null;
    }
}
