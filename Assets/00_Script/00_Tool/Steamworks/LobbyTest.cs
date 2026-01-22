using UnityEngine;
using Steamworks;

public class LobbyTest : MonoBehaviour
{
    private Callback<LobbyCreated_t> cbLobbyCreated;
    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyEnter_t> cbLobbyEntered;

    private CSteamID currentLobby;

    void Awake()
    {
        cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        cbLobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    // ★追加：外部(UI)からロビーID指定で参加できるようにする
    public void JoinLobbyById(ulong lobbyId)
    {
        if (lobbyId == 0)
        {
            Debug.LogError("[LobbyTest] lobbyId is 0");
            return;
        }

        var lobby = new CSteamID(lobbyId);
        Debug.Log("[LobbyTest] JoinLobbyById: " + lobby);
        SteamMatchmaking.JoinLobby(lobby);
    }

    public CSteamID GetCurrentLobby() => currentLobby;

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 260, 420, 200), GUI.skin.box);

        GUILayout.Label("Lobby Test");

        // ★Steam未初期化なら押せない & メッセージ表示
        if (!SteamBootstrap.IsReady)
        {
            GUILayout.Label("Steamworks未初期化（Steamクライアントから起動 / steam_appid.txt を確認）");
            GUI.enabled = false;
        }

        if (GUILayout.Button("ホスト：Lobby作成（Friends Only）", GUILayout.Height(35)))
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2);
        }

        GUI.enabled = true; // ★戻す

        if (currentLobby.m_SteamID != 0)
        {
            GUILayout.Label("Lobby ID: " + currentLobby);
            GUILayout.Label("Owner: " + SteamMatchmaking.GetLobbyOwner(currentLobby));
            GUILayout.Label("Members: " + SteamMatchmaking.GetNumLobbyMembers(currentLobby));

            if (GUILayout.Button("Lobby ID をコピー", GUILayout.Height(28)))
            {
                GUIUtility.systemCopyBuffer = currentLobby.m_SteamID.ToString();
                Debug.Log("[LobbyTest] Copied Lobby ID: " + currentLobby.m_SteamID);
            }
        }
        else
        {
            GUILayout.Label("Lobby未参加");
            GUILayout.Label("招待参加：Steamオーバーレイから招待→参加してOK");
        }

        GUILayout.EndArea();
    }

    private void OnLobbyCreated(LobbyCreated_t data)
    {
        if (data.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("CreateLobby failed: " + data.m_eResult);
            return;
        }

        currentLobby = new CSteamID(data.m_ulSteamIDLobby);
        Debug.Log("Lobby Created: " + currentLobby);

        SteamMatchmaking.SetLobbyData(currentLobby, "host", SteamUser.GetSteamID().ToString());
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.Log("JoinRequested. Lobby: " + data.m_steamIDLobby);
        SteamMatchmaking.JoinLobby(data.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t data)
    {
        currentLobby = new CSteamID(data.m_ulSteamIDLobby);
        Debug.Log("Lobby Entered: " + currentLobby);

        string hostStr = SteamMatchmaking.GetLobbyData(currentLobby, "host");
        Debug.Log("Lobby host data: " + hostStr);
    }
}
