using FishyFlip;
using FishyFlip.Models;
using Serilog;
using Serilog.Extensions.Logging;

namespace ImSky.Api;

public class AtProtoService(Config config) {
    public ATProtocol AtProtocol = new ATProtocolBuilder()
        .EnableAutoRenewSession(true)
        .WithInstanceUrl(new Uri(config.Pds))
        /*.WithLogger(
            new SerilogLoggerFactory(Log.Logger)
                .CreateLogger("FishyFlip")
        )*/
        .Build();
    public Session? Session;

    public async Task LogIn(string handle, string password) {
        if (handle.StartsWith('@')) handle = handle[1..];

        var result = await AtProtocol.Server.CreateSessionAsync(handle, password);
        if (result.IsT0) {
            this.Session = result.AsT0;
        } else {
            var err = result.AsT1;
            throw new Exception($"Failed to log in: {err.StatusCode} {err.Detail}");
        }
    }
}
