using GL.Network.Application;
using GL.Network.Application.Ports;
using GL.Network.Domain;
using UnityEngine;

namespace GL.Network.Presentation
{
    public sealed class OnlineBootstrapMono : MonoBehaviour
    {
        public OnlineRuntime Runtime { get; private set; }
        public OnlineFacade Facade => Runtime?.Facade;

        void Awake()
        {
            var (pump, lobby, transport) = OnlineFactory.Create();

            var facade = new OnlineFacade(lobby, transport);
            Runtime = new OnlineRuntime(pump, facade);
        }

        void Update()
        {
            Runtime?.Tick();
        }

        void OnApplicationQuit()
        {
            Runtime?.Shutdown();
        }
    }

    internal static class OnlineFactory
    {
        public static (INetworkPump pump, ILobbyService lobby, ITransport transport) Create()
        {
#if ONLINE_STEAM
            var pump = new GL.Network.Infrastructure.Steam.SteamClient();
            var lobby = new GL.Network.Infrastructure.Steam.SteamLobbyService((GL.Network.Infrastructure.Steam.SteamClient)pump);
            var transport = new GL.Network.Infrastructure.Steam.SteamTransport();
            return (pump, lobby, transport);

#elif ONLINE_AWS
            var pump = new Online.Infrastructure.Aws.AwsPump();
            var lobby = new Online.Infrastructure.Aws.AwsLobbyService();
            var transport = new Online.Infrastructure.Aws.AwsTransport();
            return (pump, lobby, transport);

#else
            var pump = new GL.Network.Infrastructure.Null.NullPump();
            var lobby = new GL.Network.Infrastructure.Null.NullLobbyService();
            var transport = new GL.Network.Infrastructure.Null.NullTransport();
            return (pump, lobby, transport);
#endif
        }
    }
}
