using FishyFlip;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;

namespace ImSky.Api;

public class AtProtoService(Config config, ILogger<AtProtoService> logger) {
    public ATProtocol AtProtocol = BuildProtocol(config.Pds);

    public static ATProtocol BuildProtocol(string pds) => new ATProtocolBuilder()
        .EnableAutoRenewSession(true)
        .WithInstanceUrl(new Uri(pds))
        .WithUserAgent($"ImSky/{Program.Version} (https://github.com/NotNite/ImSky)")
        /*.WithLogger(
            new SerilogLoggerFactory(Log.Logger)
                .CreateLogger("FishyFlip")
        )*/
        .Build();

    // TODO: rewrite with oauth
    public async Task<Session> LoginWithSession(Session session) {
        logger.LogInformation("Attempting to login with session");

        var refresh = await this.AtProtocol.Server.RefreshSessionAsync(session);
        if (refresh.IsT0) {
            logger.LogInformation("Logged in to {Handle} with session", session.Handle);
            return refresh.AsT0;
        }

        var err = refresh.AsT1;
        throw new Exception($"Failed to log in: {err.StatusCode} {err.Detail}");
    }

    public async Task<Session> LoginWithPassword(string handle, string password) {
        if (handle.StartsWith('@')) handle = handle[1..];
        logger.LogInformation("Attempting to login with password");

        var login = await this.AtProtocol.Server.CreateSessionAsync(handle, password);
        if (login.IsT0) {
            logger.LogInformation("Logged in to {Handle} with password", handle);
            return login.AsT0;
        }

        var err = login.AsT1;
        throw new Exception($"Failed to log in: {err.StatusCode} {err.Detail}");
    }
}
