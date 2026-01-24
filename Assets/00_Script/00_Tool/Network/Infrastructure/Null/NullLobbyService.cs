using System;
using System.Collections.Generic;
using GL.Network.Application.Ports;
using GL.Network.Domain;

namespace GL.Network.Infrastructure.Null
{
    public sealed class NullLobbyService : ILobbyService
    {
        public bool IsReady => true;
        public bool IsInLobby => false;
        public LobbyId CurrentLobby => default;

        public event Action<LobbyId> OnEntered;
        public event Action OnLeft;

        public void SetLocalDisplayName(string name)
        {
            // no-op
        }

        public void CreateFriendsOnly(int maxMembers)
        {
            // no-op
        }

        public void Join(LobbyId lobbyId)
        {
            // Null環境でも「入ったことにする」イベントだけ投げる
            OnEntered?.Invoke(lobbyId);
        }

        public void Leave()
        {
            // Null環境でも「出たことにする」イベントだけ投げる
            OnLeft?.Invoke();
        }

        public void RequestLobbies(Action<IReadOnlyList<LobbyInfo>> onResult, Action<string> onError)
        {
            // Null実装なので常に空
            onResult?.Invoke(Array.Empty<LobbyInfo>());
        }

        public string GetMemberDisplayName(PlayerId id)
        {
            return id.ToString();
        }

        // ロビーが存在しないので常にfalse
        public bool IsMember(PlayerId id)
        {
            return false;
        }

        // 常に空
        public IReadOnlyList<PlayerId> GetMembers()
        {
            return Array.Empty<PlayerId>();
        }
    }
}
