#if ONLINE_STEAM
using GL.Network.Application.Ports;
using Steamworks;

namespace GL.Network.Infrastructure.Steam
{
    public sealed class SteamClient : INetworkPump
    {
        public bool IsReady { get; private set; }

        public SteamClient()
        {
            try
            {
                IsReady = SteamAPI.Init();
            }
            catch
            {
                IsReady = false;
            }
        }

        public void Tick()
        {
            if (IsReady) SteamAPI.RunCallbacks();

            //UnityEngine.Debug.Log("[SteamClient] RunCallbacks");
        }

        public void Shutdown()
        {
            if (!IsReady) return;
            SteamAPI.Shutdown();
            IsReady = false;

        }
    }
}
#endif
