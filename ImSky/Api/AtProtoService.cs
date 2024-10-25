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

        var authSession = new AuthSession(session);
        var refresh = await this.AtProtocol.AuthenticateWithPasswordSessionAsync(authSession);
        if (refresh != null) {
            logger.LogInformation("Logged in with session");
            return refresh;
        }

        throw new Exception("Failed to log in");
    }

    public async Task<Session> LoginWithPassword(string handle, string password) {
        if (handle.StartsWith('@')) handle = handle[1..];
        logger.LogInformation("Attempting to login with password");

        var login = await this.AtProtocol.AuthenticateWithPasswordAsync(handle, password);
        if (login != null) {
            logger.LogInformation("Logged in to {Handle} with password", handle);
            return login;
        }

        throw new Exception("Failed to log in");
    }

    public void LogOut() {
        this.AtProtocol = BuildProtocol(config.Pds);
    }
}
