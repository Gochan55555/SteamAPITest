#if ONLINE_STEAM
using System;
using System.Collections.Generic;
using GL.Network.Application.Ports;
using GL.Network.Domain;
using Steamworks;
using UnityEngine;

namespace GL.Network.Infrastructure.Steam
{
    public sealed class SteamLobbyService : ILobbyService
    {
        private readonly SteamClient _client;

        private Callback<LobbyCreated_t> _cbCreated;
        private Callback<LobbyEnter_t> _cbEnter;
        private Callback<GameLobbyJoinRequested_t> _cbJoinReq;

        private LobbyId _current;
        private string _localName = "Player";

        public SteamLobbyService(SteamClient client)
        {
            _client = client;

            _cbCreated = Callback<LobbyCreated_t>.Create(OnCreated);
            _cbEnter = Callback<LobbyEnter_t>.Create(OnEnteredInternal);
            _cbJoinReq = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        }

        public bool IsReady => _client != null && _client.IsReady;
        public bool IsInLobby => _current.IsValid;
        public LobbyId CurrentLobby => _current;

        public event Action<LobbyId> OnEntered;
        public event Action OnLeft;

        public void SetLocalDisplayName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) _localName = name.Trim();
            ApplyLocalName();
        }

        public void CreateFriendsOnly(int maxMembers)
        {
            if (!IsReady) return;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
        }

        public void Join(LobbyId lobbyId)
        {
            if (!IsReady || !lobbyId.IsValid) return;
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId.Value));
        }

        public void Leave()
        {
            if (!IsReady || !_current.IsValid) return;
            SteamMatchmaking.LeaveLobby(new CSteamID(_current.Value));
            _current = default;
            OnLeft?.Invoke();
        }

        public void RequestLobbies(Action<IReadOnlyList<LobbyInfo>> onResult, Action<string> onError)
        {
            if (!IsReady) { onError?.Invoke("Steam not ready"); return; }

            // サンプル：全世界 + フレンドだけ（必要なら条件は UseCase へ寄せる）
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

            var handle = SteamMatchmaking.RequestLobbyList();

            var cr = CallResult<LobbyMatchList_t>.Create((data, ioFail) =>
            {
                if (ioFail) { onError?.Invoke("RequestLobbyList IO fail"); return; }

                var list = new List<LobbyInfo>();
                int count = (int)data.m_nLobbiesMatching;
                for (int i = 0; i < count; i++)
                {
                    var lob = SteamMatchmaking.GetLobbyByIndex(i);
                    var owner = SteamMatchmaking.GetLobbyOwner(lob);

                    if (SteamFriends.GetFriendRelationship(owner) != EFriendRelationship.k_EFriendRelationshipFriend)
                        continue;

                    list.Add(new LobbyInfo(
                        new LobbyId(lob.m_SteamID),
                        new PlayerId(owner.m_SteamID),
                        SteamFriends.GetFriendPersonaName(owner),
                        SteamMatchmaking.GetNumLobbyMembers(lob)
                    ));
                }

                onResult?.Invoke(list);
            });
            cr.Set(handle);
        }

        public void SendLobbyChat(string text)
        {
            if (!IsReady || !_current.IsValid) return;
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            SteamMatchmaking.SendLobbyChatMsg(new CSteamID(_current.Value), bytes, bytes.Length);
        }

        public string GetMemberDisplayName(PlayerId id)
        {
            if (!IsReady) return id.ToString();

            if (_current.IsValid)
            {
                var nick = SteamMatchmaking.GetLobbyMemberData(
                    new CSteamID(_current.Value),
                    new CSteamID(id.Value),
                    "nick"
                );
                if (!string.IsNullOrEmpty(nick)) return nick;
            }

            var name = SteamFriends.GetFriendPersonaName(new CSteamID(id.Value));
            return string.IsNullOrEmpty(name) ? id.ToString() : name;
        }

        // ---- callbacks ----

        private void OnCreated(LobbyCreated_t data)
        {
            if (data.m_eResult != EResult.k_EResultOK) return;

            _current = new LobbyId(data.m_ulSteamIDLobby);

            // ロビーデータ（例）
            var lob = new CSteamID(_current.Value);
            SteamMatchmaking.SetLobbyData(lob, "host", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(lob, "ver", UnityEngine.Application.version);

            ApplyLocalName();
            OnEntered?.Invoke(_current);
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t data)
        {
            Join(new LobbyId(data.m_steamIDLobby.m_SteamID));
        }

        private void OnEnteredInternal(LobbyEnter_t data)
        {
            var lobbyId = new LobbyId(data.m_ulSteamIDLobby);
            var resp = (EChatRoomEnterResponse)data.m_EChatRoomEnterResponse;
            if (resp != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) return;

            _current = lobbyId;
            ApplyLocalName();
            OnEntered?.Invoke(_current);
        }

        private void ApplyLocalName()
        {
            if (!IsReady || !_current.IsValid) return;
            SteamMatchmaking.SetLobbyMemberData(new CSteamID(_current.Value), "nick", _localName);
        }
    }
}
#endif
