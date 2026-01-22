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
    [Header("Optional UI")]
    [SerializeField] private bool showOnGUI = true;      // ★単体UIを出したい時だけON
    [SerializeField] private Rect uiRect = new Rect(10, 200, 420, 260); // ★被らない位置にしてある
    [SerializeField] private LobbyTest lobby;             // ★Joinボタンを使うなら必要
    private Vector2 scroll;
    public List<LobbyInfo> Results { get; private set; } = new();
    public bool IsRequesting { get; private set; }
    void OnGUI()
    {
        if (!showOnGUI) return;

        GUILayout.BeginArea(uiRect, GUI.skin.box);
        GUILayout.Label("Lobby Browser (Friends)");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Height(28)))
            RefreshFriendLobbies();
        GUILayout.Label(IsRequesting ? "Searching..." : "");
        GUILayout.EndHorizontal();

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(170));
        foreach (var l in Results)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.Label($"{l.OwnerName}  members:{l.Members}", GUILayout.Width(260));
            GUILayout.Label(l.LobbyId.m_SteamID.ToString(), GUILayout.Width(120));

            GUI.enabled = (lobby != null);
            if (GUILayout.Button("Join", GUILayout.Width(60)))
            {
                lobby.JoinLobbyById(l.LobbyId.m_SteamID);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (lobby == null)
            GUILayout.Label("※Joinボタンを使うなら Inspector で LobbyTest を設定して");

        GUILayout.EndArea();
    }
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

        int count = (int)data.m_nLobbiesMatching;

        for (int i = 0; i < count; i++)
        {
            var lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            var owner = SteamMatchmaking.GetLobbyOwner(lobbyId);

            // Ownerがフレンドじゃないロビーは除外
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
