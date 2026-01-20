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
        // コールバック登録
        cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        cbLobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 420, 200), GUI.skin.box);

        GUILayout.Label("Lobby Test");

        if (GUILayout.Button("ホスト：Lobby作成（Friends Only）", GUILayout.Height(35)))
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2);
        }

        if (currentLobby.m_SteamID != 0)
        {
            GUILayout.Label("Lobby ID: " + currentLobby);
            GUILayout.Label("Owner: " + SteamMatchmaking.GetLobbyOwner(currentLobby));
            GUILayout.Label("Members: " + SteamMatchmaking.GetNumLobbyMembers(currentLobby));
        }
        else
        {
            GUILayout.Label("Lobby未参加");
            GUILayout.Label("フレンド招待で参加する場合は、Steamオーバーレイから招待→参加してOK");
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

        // 例：ロビーにホストIDを入れておく（参加者が参照する用）
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

        // ここで次のP2P接続処理に進む（次のスクリプトでやる）
    }

    public CSteamID GetCurrentLobby() => currentLobby;
}
