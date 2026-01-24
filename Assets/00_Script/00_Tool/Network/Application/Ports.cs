using GL.Network.Domain;
using System;
using System.Collections.Generic;

namespace GL.Network.Application.Ports
{
    public readonly struct LobbyInfo
    {
        public readonly LobbyId LobbyId;
        public readonly PlayerId Owner;
        public readonly string OwnerName;
        public readonly int Members;

        public LobbyInfo(LobbyId lobbyId, PlayerId owner, string ownerName, int members)
        {
            LobbyId = lobbyId;
            Owner = owner;
            OwnerName = ownerName;
            Members = members;
        }
    }

    public interface ILobbyService
    {
        bool IsReady { get; }
        bool IsInLobby { get; }
        LobbyId CurrentLobby { get; }

        event Action<LobbyId> OnEntered;
        event Action OnLeft;
        event System.Action<GL.Network.Domain.PlayerId, string> OnLobbyChat;

        void SetLocalDisplayName(string name);

        void CreateFriendsOnly(int maxMembers);
        void Join(LobbyId lobbyId);
        void Leave();

        void RequestLobbies(Action<IReadOnlyList<LobbyInfo>> onResult, Action<string> onError);

        // ロビーのテキストチャット（SteamのLobbyChat等の実装にできる）
        void SendLobbyChat(string text);

        // 名前引き（実装側で “nick” を持つ/持たないは自由）
        string GetMemberDisplayName(PlayerId id);
    }

    public interface ITransport
    {
        void Send(PlayerId to, NetEnvelope env, SendReliability reliability);

        // Spanではなく配列で受ける
        int Receive(NetReceived[] buffer);

        public readonly struct NetReceived
        {
            public readonly PlayerId From;
            public readonly NetEnvelope Envelope;
            public NetReceived(PlayerId from, NetEnvelope env) { From = from; Envelope = env; }
        }
    }    // SteamAPI.RunCallbacks / AWSポーリング / WebSocket pump 等を隠す
    
    public interface INetworkPump
    {
        bool IsReady { get; }
        void Tick();
        void Shutdown();
    }
}
