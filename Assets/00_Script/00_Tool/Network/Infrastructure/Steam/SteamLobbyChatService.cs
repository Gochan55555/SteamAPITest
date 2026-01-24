#if ONLINE_STEAM
using System;
using GL.Network.Application.Ports;
using GL.Network.Domain;
using Steamworks;

namespace GL.Network.Infrastructure.Steam
{
    public sealed class SteamLobbyChatService : IChatService
    {
        private readonly SteamLobbyService _lobby;
        private readonly Callback<LobbyChatMsg_t> _cbChatMsg;

        public bool IsReady => _lobby != null && _lobby.IsReady;

        public event Action<PlayerId, string> OnMessage;

        public SteamLobbyChatService(SteamLobbyService lobby)
        {
            _lobby = lobby;
            _cbChatMsg = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMsg);
        }

        public void Connect(string roomId)
        {
            // Steam Lobby Chat は「参加中ロビー」を購読するだけなので何もしない
        }

        public void Disconnect()
        {
        }

        public void Send(string text)
        {
            if (!IsReady || !_lobby.IsInLobby) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            SteamMatchmaking.SendLobbyChatMsg(new CSteamID(_lobby.CurrentLobby.Value), bytes, bytes.Length);
        }

        private void OnLobbyChatMsg(LobbyChatMsg_t data)
        {
            if (!_lobby.IsInLobby) return;
            if (data.m_ulSteamIDLobby != _lobby.CurrentLobby.Value) return;

            CSteamID user;
            EChatEntryType type;
            byte[] buffer = new byte[4096];

            int len = SteamMatchmaking.GetLobbyChatEntry(
                new CSteamID(data.m_ulSteamIDLobby),
                (int)data.m_iChatID,
                out user,
                buffer,
                buffer.Length,
                out type
            );

            if (len <= 0) return;
            if (type != EChatEntryType.k_EChatEntryTypeChatMsg) return;

            string text = System.Text.Encoding.UTF8.GetString(buffer, 0, len);
            OnMessage?.Invoke(new PlayerId(user.m_SteamID), text);
        }
    }
}
#endif
