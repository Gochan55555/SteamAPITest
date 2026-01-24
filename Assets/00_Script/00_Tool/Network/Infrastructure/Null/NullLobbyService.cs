using GL.Network.Application.Ports;
using GL.Network.Domain;
using System;
using System.Collections.Generic;

namespace GL.Network.Infrastructure.Null
{
    public sealed class NullLobbyService : ILobbyService
    {
        public bool IsReady => true;
        public bool IsInLobby => _current.IsValid;
        public LobbyId CurrentLobby => _current;

        public event Action<LobbyId> OnEntered;
        public event Action OnLeft;

        private LobbyId _current;
        private string _name = "Player";

        public void SetLocalDisplayName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) _name = name.Trim();
        }

        public void CreateFriendsOnly(int maxMembers)
        {
            _current = new LobbyId(1);
            OnEntered?.Invoke(_current);
        }
        public event Action<PlayerId, string> OnLobbyChat;

        public void Join(LobbyId lobbyId)
        {
            _current = lobbyId.IsValid ? lobbyId : new LobbyId(1);
            OnEntered?.Invoke(_current);
        }

        public void Leave()
        {
            _current = default;
            OnLeft?.Invoke();
        }

        public void RequestLobbies(Action<IReadOnlyList<LobbyInfo>> onResult, Action<string> onError)
        {
            onResult?.Invoke(new List<LobbyInfo>());
        }

        public void SendLobbyChat(string text) { }

        public string GetMemberDisplayName(PlayerId id) => _name;
    }
}
