using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Serilog;

namespace ImSky;

public class Config : IDisposable {
    [JsonIgnore]
    public static string ConfigPath => Path.Combine(
        Program.AppDir,
        "config.json"
    );

    // UI
    [JsonInclude] public int WindowX = 100;
    [JsonInclude] public int WindowY = 100;
    [JsonInclude] public int WindowWidth = 540;
    [JsonInclude] public int WindowHeight = 960;

    [JsonInclude] public string Pds = "https://bsky.social";
    [JsonInclude] public string? Handle;
    [JsonInclude] public string? Password;
    [JsonInclude] public bool SavePassword;
    [JsonInclude] public bool AutoLogin = true;

    public static Config Load() {
        Config config;

        try {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath))!;
        } catch (Exception e) {
            Log.Warning(e, "Failed to load config file - creating a new one");
            config = new Config();
        }

        config.Fixup();
        config.Save();

        return config;
    }

    public void Fixup() {
        if (this.Password is not null && !this.SavePassword) {
            this.Password = null;
        }
    }

    public void Save() {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions {
            WriteIndented = true
        }));
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
