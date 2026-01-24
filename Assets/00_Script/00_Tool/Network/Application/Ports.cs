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

    // =========================
    // Match / Lobby (マッチング)
    // =========================
    public interface ILobbyService
    {
        bool IsReady { get; }
        bool IsInLobby { get; }
        LobbyId CurrentLobby { get; }

        event Action<LobbyId> OnEntered;
        event Action OnLeft;

        void SetLocalDisplayName(string name);

        void CreateFriendsOnly(int maxMembers);
        void Join(LobbyId lobbyId);
        void Leave();

        void RequestLobbies(Action<IReadOnlyList<LobbyInfo>> onResult, Action<string> onError);

        // 名前引き（実装側で “nick” を持つ/持たないは自由）
        string GetMemberDisplayName(PlayerId id);

        // ✅ 追加：ロビー所属チェック（受信フィルタで使う）
        bool IsMember(PlayerId id);

        // ✅ 追加：メンバー一覧（AcceptSessionなどで使う）
        IReadOnlyList<PlayerId> GetMembers();
    }

    // =========================
    // Chat (チャット)
    // =========================
    public interface IChatService
    {
        bool IsReady { get; }

        /// <summary>
        /// ルームに接続（Steam Lobby Chatなら概念上接続だけ）
        /// </summary>
        void Connect(string roomId);

        void Disconnect();

        void Send(string text);

        /// <summary>
        /// (from, text)
        /// </summary>
        event Action<PlayerId, string> OnMessage;
    }

    // =========================
    // Game Transport (ゲーム通信)
    // =========================
    public interface ITransport
    {
        void Send(PlayerId to, NetEnvelope env, SendReliability reliability);

        // 配列で受信（unsafe/Span回避）
        int Receive(ITransport.NetReceived[] buffer);

        public readonly struct NetReceived
        {
            public readonly PlayerId From;
            public readonly NetEnvelope Envelope;

            public NetReceived(PlayerId from, NetEnvelope env)
            {
                From = from;
                Envelope = env;
            }
        }
    }

    public interface INetworkPump
    {
        bool IsReady { get; }
        void Tick();
        void Shutdown();
    }
}
