using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Serilog;

namespace ImSky;

public class Config : IDisposable {
    public const string DefaultPds = "https://bsky.social";
    public const string DefaultLanguage = "en";

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(
        Program.AppDir,
        "config.json"
    );

    [JsonIgnore]
    private static JsonSerializerOptions SerializerOptions => new() {
        WriteIndented = true,
        IncludeFields = true
    };

    // UI
    public Vector2 WindowPos = new(100, 100);
    public Vector2 WindowSize = new(540, 960);

    // Login
    public string Pds = DefaultPds;
    public string? Handle;
    public string? Password;
    public string? Language = DefaultLanguage;
    public bool AutoLogin = true;

    public static Config Load() {
        Config config;

        try {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), SerializerOptions)!;
        } catch (Exception e) {
            Log.Warning(e, "Failed to load config file - creating a new one");
            config = new Config();
        }

        config.Fixup();
        config.Save();

        return config;
    }

    public void Fixup() {
        // TODO
    }

    public void Save() {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public void Dispose() {
        // If we edited the config file on disk, skip saving it so we don't overwrite it
        try {
            var newConfig = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(ConfigPath))!;
            var oldConfig = JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(this))!;
            if (!JsonNode.DeepEquals(newConfig, oldConfig)) return;
        } catch {
            // ignored
        }

        this.Save();
    }
}
