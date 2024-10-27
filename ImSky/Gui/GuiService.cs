using System.Diagnostics;
using System.Numerics;
using Hexa.NET.ImGui;
using ImSky.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

#pragma warning disable CS0162 // Unreachable code detected

namespace ImSky;

public class GuiService(Config config, ILogger<GuiService> logger) : IHostedService {
    public const bool ShowImGuiDebug = false;

    private Task task = null!;
    private CancellationTokenSource cts = null!;
    private readonly string iniPath = Path.Combine(Program.AppDir, "imgui.ini");

    private Sdl sdl = null!;

    private readonly HttpClient client = new();
    private readonly Dictionary<string, Texture> textures = new();

    private View? currentView;
    private View? queuedView;
    private string windowName = string.Empty;
    private ImGuiWrapper imgui = null!;

    public Task StartAsync(CancellationToken cancellationToken) {
        this.cts = new CancellationTokenSource();
        this.task = Task.Run(this.Run, this.cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        this.cts.Cancel();
        return this.task;
    }

    public View? GetView() => this.currentView;

    public T SetView<T>() where T : View {
        var view = Program.Host.Services.GetRequiredService<T>();
        this.queuedView = view;
        return view;
    }

    public T SetView<T>(T view) where T : View {
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

    public async Task Run() {
        this.imgui = new ImGuiWrapper("ImSky", config.WindowPos, config.WindowSize, this.iniPath);

        var stopwatch = Stopwatch.StartNew();
        while (!this.imgui.Exiting && !this.cts.Token.IsCancellationRequested) {
            stopwatch.Restart();

            this.imgui.DoEvents();
            if (this.imgui.Exiting) break;

            lock (this.imgui) {
                this.ProcessTextures();
                this.imgui.Render(() => {
                    try {
                        this.Draw();
                    } catch (Exception e) {
                        logger.LogError(e, "Error in draw loop");
                    }
                });
            }
        }

        config.WindowPos = this.imgui.WindowPos;
        config.WindowSize = this.imgui.WindowSize;
        config.Save();

        ImGui.SaveIniSettingsToDisk(this.iniPath);
        this.imgui.Dispose();
        _ = Task.Run(async () => await Program.Host.StopAsync());
    }

    private void Draw() {
        if (this.currentView == null) {
            var loginView = this.SetView<LoginView>();
            loginView.IsInitial = true;
        }
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
                    //logger.LogDebug("Loaded texture from {Url}", url);

                    var img = Image.Load(data);
                    var rgba = img.CloneAs<Rgba32>();

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
                if (value.Handle is not null) this.imgui.DestroyTexture(value.Handle.Value);
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
        var (data, width, height) =
            texWrapper.CreationData ?? throw new InvalidOperationException("No creation data");
        texWrapper.Handle = this.imgui.CreateTexture(data, (int) width, (int) height);
        texWrapper.CreationData = null;
    }
}
