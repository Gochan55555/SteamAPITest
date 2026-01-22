using UnityEngine;
using Steamworks;

public class SteamLobbyUI : MonoBehaviour
{
    private enum Mode { Home, Host, Guest }

    [Header("Refs")]
    [SerializeField] private LobbyTest lobby;
    [SerializeField] private LobbyBrowser browser;
    [SerializeField] private P2PTest p2p;

    private Mode mode = Mode.Home;

    private string manualLobbyId = "";
    private string status = "";
    private string joinStateText = "";
    private bool joinInProgress;
    private float joinStartTime;
    private const float JoinTimeoutSec = 10f;

    private Vector2 lobbyListScroll;
    private string nameInput = "";

    void OnEnable()
    {
        nameInput = PlayerPrefs.GetString("LOBBY_NICK", "Player");

        if (lobby != null)
            lobby.OnJoinResultEvent += OnJoinResult;
    }

    void OnDisable()
    {
        if (lobby != null)
        {
            lobby.OnJoinResultEvent -= OnJoinResult;
        }
    }

    void Update()
    {
        if (joinInProgress && (Time.unscaledTime - joinStartTime) > JoinTimeoutSec)
        {
            joinInProgress = false;
            joinStateText = "参加結果が返ってきませんでした（タイムアウト）";
            status = "参加失敗扱い";
        }
    }

    void OnGUI()
    {
        // ★他OnGUIが汚したGUI状態を強制リセット（これが効く）
        GUI.enabled = true;
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.contentColor = Color.white;

        // 画面サイズに追従（固定560はやめる）
        float margin = 10f;
        float w = Mathf.Min(420f, (Screen.width - margin * 3f) * 0.5f);
        float h = Screen.height - margin * 2f;

        var left = new Rect(margin, margin, w, h);
        var right = new Rect(margin + w + margin, margin, w, h);

        DrawLeft(left);
        DrawRight(right);
    }

    private void DrawLeft(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Steam Lobby UI");

        if (!SteamBootstrap.IsReady)
        {
            GUILayout.Label("Steam未初期化：Steamクライアントから起動してね");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Home")) mode = Mode.Home;
        if (GUILayout.Button("Host")) mode = Mode.Host;
        if (GUILayout.Button("Guest")) mode = Mode.Guest;
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (mode == Mode.Home)
        {
            GUILayout.Label("Host: ロビー作成して待つ");
            GUILayout.Label("Guest: フレンドが建てたロビーを検索して参加");
        }
        else if (mode == Mode.Host)
        {
            DrawHostPanel();
        }
        else
        {
            DrawGuestPanel();
        }

        GUILayout.Space(8);
        GUILayout.Label("Status: " + status);
        GUILayout.Label("Join Status: " + joinStateText);

        GUILayout.EndArea();
    }

    private void DrawHostPanel()
    {
        GUILayout.Label("Host Panel");

        GUI.enabled = !(lobby != null && lobby.IsInLobby);
        if (GUILayout.Button("Create Lobby (Friends Only)", GUILayout.Height(36)))
        {
            lobby.CreateFriendsOnlyLobby(4);
            status = "ロビー作成中...";
        }
        GUI.enabled = true;

        if (lobby != null && lobby.IsInLobby)
        {
            GUILayout.Label("Lobby ID: " + lobby.CurrentLobby.m_SteamID);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Lobby ID", GUILayout.Height(28)))
            {
                GUIUtility.systemCopyBuffer = lobby.CurrentLobby.m_SteamID.ToString();
                status = "Lobby ID をコピーしました";
            }

            if (GUILayout.Button("Leave Lobby", GUILayout.Height(28)))
            {
                lobby.LeaveLobby();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Members: " +
                SteamMatchmaking.GetNumLobbyMembers(lobby.CurrentLobby));
        }
    }

    private void DrawGuestPanel()
    {
        GUILayout.Label("Guest Panel");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Search Friend Lobbies", GUILayout.Height(32)))
        {
            browser.RefreshFriendLobbies();
            status = "検索中...";
        }
        if (browser.IsRequesting) GUILayout.Label("...");
        GUILayout.EndHorizontal();

        lobbyListScroll = GUILayout.BeginScrollView(lobbyListScroll, GUILayout.Height(220));
        foreach (var l in browser.Results)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"{l.OwnerName}  members:{l.Members}", GUILayout.Width(260));
            if (GUILayout.Button("Join", GUILayout.Width(80)))
            {
                StartJoin(l.LobbyId.m_SteamID);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);

        GUILayout.Label("Manual Join (Lobby ID)");
        GUILayout.BeginHorizontal();
        manualLobbyId = GUILayout.TextField(manualLobbyId, GUILayout.Width(260));
        if (GUILayout.Button("Paste", GUILayout.Width(60)))
            manualLobbyId = GUIUtility.systemCopyBuffer ?? "";
        if (GUILayout.Button("Join", GUILayout.Width(60)))
            TryJoinManual();
        GUILayout.EndHorizontal();

        if (lobby != null && lobby.IsInLobby)
        {
            if (GUILayout.Button("Leave Lobby", GUILayout.Height(28)))
                lobby.LeaveLobby();
        }
    }

    private void TryJoinManual()
    {
        if (!ulong.TryParse(manualLobbyId.Trim(), out var id) || id == 0)
        {
            status = "Lobby ID が不正";
            return;
        }
        StartJoin(id);
    }

    private void StartJoin(ulong lobbyId)
    {
        lobby.JoinLobbyById(lobbyId);

        joinInProgress = true;
        joinStartTime = Time.unscaledTime;
        joinStateText = "参加中...";
        status = "参加リクエスト送信: " + lobbyId;
    }

    private void OnJoinResult(bool success, string message, CSteamID lobbyId)
    {
        joinInProgress = false;
        joinStateText = message + $" (Lobby:{lobbyId.m_SteamID})";
        status = success ? "参加成功" : "参加失敗";
    }

    private void DrawRight(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);

        if (p2p == null)
        {
            GUILayout.Label("Chat");
            GUILayout.Label("ERROR: P2PTest が未設定");
            GUILayout.EndArea();
            return;
        }

        if (lobby == null)
        {
            GUILayout.Label("Chat");
            GUILayout.Label("ERROR: LobbyTest が未設定");
            GUILayout.EndArea();
            return;
        }

        // ★ここでチャットUI描画（中でBeginAreaしない版）
        p2p.DrawChatUI(rect);

        GUILayout.EndArea();
    }
}
