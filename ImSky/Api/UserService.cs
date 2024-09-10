using FishyFlip.Models;
using FishyFlip.Tools;
using ImSky.Models;

namespace ImSky.Api;

public class UsersService(AtProtoService atProto) {
    public async Task<User> GetUserProfile(string handleStr) {
        var handle = ATHandle.Create(handleStr);
        if (handle is null) throw new Exception("Failed to lookup handle");
        var profile = (await atProto.AtProtocol.Actor.GetProfileAsync(handle)).HandleResult();
        if (profile is null) throw new Exception("Failed to lookup profile");
        return new User(profile);
    }
}
