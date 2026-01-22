using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class LobbyBrowser : MonoBehaviour
{
    private CallResult<LobbyMatchList_t> crLobbyMatchList;

    public struct LobbyInfo
    {
        public CSteamID LobbyId;
        public CSteamID Owner;
        public string OwnerName;
        public int Members;
    }

    public List<LobbyInfo> Results { get; private set; } = new();
    public bool IsRequesting { get; private set; }

    public void RefreshFriendLobbies()
    {
        if (!SteamBootstrap.IsReady)
        {
            Debug.LogError("[LobbyBrowser] Steam not ready");
            return;
        }

        Results.Clear();
        IsRequesting = true;

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        var h = SteamMatchmaking.RequestLobbyList();
        crLobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        crLobbyMatchList.Set(h);
    }

    private void OnLobbyMatchList(LobbyMatchList_t data, bool ioFailure)
    {
        IsRequesting = false;

        if (ioFailure)
        {
            Debug.LogError("[LobbyBrowser] RequestLobbyList IO failure");
            return;
        }

        // ÅöÇ±Ç±Ç™èCê≥ì_Åiuint -> intÅj
        int count = (int)data.m_nLobbiesMatching;

        for (int i = 0; i < count; i++)
        {
            var lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            var owner = SteamMatchmaking.GetLobbyOwner(lobbyId);

            if (SteamFriends.GetFriendRelationship(owner) != EFriendRelationship.k_EFriendRelationshipFriend)
                continue;

            Results.Add(new LobbyInfo
            {
                LobbyId = lobbyId,
                Owner = owner,
                OwnerName = SteamFriends.GetFriendPersonaName(owner),
                Members = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
            });
        }

        Debug.Log($"[LobbyBrowser] Friend lobbies: {Results.Count}");
    }
}
