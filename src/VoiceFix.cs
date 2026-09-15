using System.Collections.Concurrent;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Fixes;

internal sealed class VoiceRateLimit
{
    private readonly Lock sync = new();
    private long windowStart = Environment.TickCount64;
    private int callCount;

    public bool IsExceeded()
    {
        lock (sync)
        {
            var now = Environment.TickCount64;
            if (now - windowStart >= 1000)
            {
                windowStart = now;
                callCount = 0;
            }

            return ++callCount > 128;
        }
    }
}

public partial class Fixes
{
    private ConcurrentDictionary<int, ulong> playerSeeds = [];
    private readonly ConcurrentDictionary<int, VoiceRateLimit> playerVoiceRateLimits = [];
    private ulong GlobalSeed = 0;
    private bool enableVoiceFix = false;

    private void InitVoiceFix()
    {
        enableVoiceFix = Config.CurrentValue.EnableVoiceFix;
        Config.OnChange((v, _) =>
        {
            enableVoiceFix = v.EnableVoiceFix;
        });
    }

    private ulong GetSeed()
    {
        if (GlobalSeed == 0)
        {
            var rng = new Random();
            byte[] buffer = new byte[8];
            rng.NextBytes(buffer);

            GlobalSeed = BitConverter.ToUInt64(buffer, 0);
        }

        GlobalSeed += 66;
        return GlobalSeed;
    }

    [EventListener<EventDelegates.OnClientConnected>]
    void ConnectListener(IOnClientConnectedEvent @event)
    {
        if (!enableVoiceFix)
            return;

        playerSeeds[@event.PlayerId] = GetSeed();
        playerVoiceRateLimits.TryRemove(@event.PlayerId, out _);
    }

    [EventListener<EventDelegates.OnMapLoad>]
    void MapLoadListener(IOnMapLoadEvent @event)
    {
        if (!enableVoiceFix)
            return;

        playerSeeds.Clear();
        playerVoiceRateLimits.Clear();
    }

    [ServerNetMessageInternalHandler]
    public HookResult OnVoiceDataSend(CSVCMsg_VoiceData msg, int playerid)
    {
        if (!enableVoiceFix)
            return HookResult.Continue;

        if (!playerSeeds.TryGetValue(playerid, out var seed))
        {
            seed = GetSeed();
            playerSeeds[playerid] = seed;
        }

        msg.Xuid = seed + (ulong)msg.Entity;
        return HookResult.Continue;
    }

    [ClientNetMessageHandler]
    public HookResult OnClientVoice(CCLCMsg_VoiceData msg, int playerid)
    {
        if (!enableVoiceFix)
            return HookResult.Continue;

        if(!msg.Accessor.HasField("audio")) return HookResult.Stop;

        var rateLimit = playerVoiceRateLimits.GetOrAdd(playerid, _ => new());
        if (rateLimit.IsExceeded())
            return HookResult.Stop;

        return HookResult.Continue;
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void VoiceFixClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        playerVoiceRateLimits.TryRemove(@event.PlayerId, out _);
    }
}
