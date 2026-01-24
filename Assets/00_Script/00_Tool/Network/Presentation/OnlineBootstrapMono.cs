using GL.Network.Application;
using GL.Network.Application.Ports;
using UnityEngine;

namespace GL.Network.Presentation
{
    public sealed class OnlineBootstrapMono : MonoBehaviour
    {
        public OnlineRuntime Runtime { get; private set; }
        public OnlineFacade Facade => Runtime?.Facade;

        void Awake()
        {
            var (pump, lobby, transport, chat) = OnlineFactory.Create();

            var facade = new OnlineFacade(lobby, transport, chat);
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
        public static (INetworkPump pump, ILobbyService lobby, ITransport transport, IChatService chat) Create()
        {
#if ONLINE_STEAM
            var pump = new GL.Network.Infrastructure.Steam.SteamClient();
            var lobby = new GL.Network.Infrastructure.Steam.SteamLobbyService(pump);
            var transport = new GL.Network.Infrastructure.Steam.SteamTransport();

            // ✅ チャット専用（新規追加したクラス）
            var chat = new GL.Network.Infrastructure.Steam.SteamLobbyChatService(lobby);

            return (pump, lobby, transport, chat);

#elif ONLINE_AWS
            var pump = new Online.Infrastructure.Aws.AwsPump();
            var lobby = new Online.Infrastructure.Aws.AwsLobbyService();
            var transport = new Online.Infrastructure.Aws.AwsTransport();

            // AWSチャットがまだ無いなら、NullChatServiceでコンパイル通す
            var chat = new GL.Network.Infrastructure.Null.NullChatService();

            return (pump, lobby, transport, chat);

#else
            var pump = new GL.Network.Infrastructure.Null.NullPump();
            var lobby = new GL.Network.Infrastructure.Null.NullLobbyService();
            var transport = new GL.Network.Infrastructure.Null.NullTransport();
            var chat = new GL.Network.Infrastructure.Null.NullChatService();

            return (pump, lobby, transport, chat);
#endif
        }
    }
}
