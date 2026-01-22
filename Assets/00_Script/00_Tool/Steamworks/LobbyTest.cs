using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class LobbyTest : MonoBehaviour
{
    private Callback<LobbyCreated_t> cbLobbyCreated;
    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyEnter_t> cbLobbyEntered;
    private Callback<LobbyChatUpdate_t> cbLobbyChatUpdate;

    private CSteamID currentLobby;

    public CSteamID CurrentLobby => currentLobby;
    public bool IsInLobby => currentLobby.m_SteamID != 0;

    public event Action<CSteamID> OnLobbyEnteredEvent;
    public event Action OnLobbyLeftEvent;

    // ★Joinの結果をUIへ返す
    public event Action<bool, string, CSteamID> OnJoinResultEvent;

    // =========================
    // ★追加：参加状態をUIが参照できるように保持
    // =========================
    public bool IsJoining { get; private set; }
    public CSteamID TargetJoinLobby { get; private set; }
    public string LastJoinMessage { get; private set; } = "";
    public bool? LastJoinSuccess { get; private set; } = null;   // null = 未確定/未実行

    [Header("Join Timeout")]
    [SerializeField] private float joinTimeoutSec = 10f;

    private float joinStartTime;

    void Awake()
    {
        cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        cbLobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        cbLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    void Update()
    {
        // ★追加：LobbyEnter_t が返ってこない時の保険（タイムアウト）
        if (IsJoining && (Time.unscaledTime - joinStartTime) > joinTimeoutSec)
        {
            IsJoining = false;
            LastJoinSuccess = false;
            LastJoinMessage = $"参加失敗: Timeout({joinTimeoutSec:0}s)";

            Debug.LogWarning("[LobbyTest] Join TIMEOUT. target=" + TargetJoinLobby);

            OnJoinResultEvent?.Invoke(false, LastJoinMessage, TargetJoinLobby);
        }
    }
    // ★仮UI（確認用）: SteamLobbyUIを置くまでの繋ぎ
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 200, 420, 220), GUI.skin.box);
        GUILayout.Label("Lobby Test (Temp UI)");

        if (!SteamBootstrap.IsReady)
        {
            GUILayout.Label("Steam not ready");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("InLobby: " + IsInLobby);
        GUILayout.Label("LobbyID: " + (IsInLobby ? CurrentLobby.m_SteamID.ToString() : "-"));
        GUILayout.Label("Join: " + (IsJoining ? "参加中..." : "待機"));
        GUILayout.Label("Last: " + LastJoinMessage);

        GUI.enabled = !IsInLobby && !IsJoining;
        if (GUILayout.Button("Create FriendsOnly Lobby", GUILayout.Height(32)))
            CreateFriendsOnlyLobby(4);
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Manual Join ID:");
        // 手入力したいなら string をフィールドで持つ必要あるから、ここではコピー運用でOK
        GUILayout.EndHorizontal();

        if (IsInLobby)
        {
            if (GUILayout.Button("Leave Lobby", GUILayout.Height(28)))
                LeaveLobby();
        }

        GUILayout.EndArea();
    }

    public void CreateFriendsOnlyLobby(int maxMembers = 4)
    {
        if (!SteamBootstrap.IsReady)
        {
            Debug.LogError("[LobbyTest] Steam not ready");
            return;
        }
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
    }

    public void JoinLobbyById(ulong lobbyId)
    {
        if (lobbyId == 0)
        {
            Debug.LogError("[LobbyTest] lobbyId is 0");
            return;
        }

        // ★追加：参加中状態へ
        IsJoining = true;
        joinStartTime = Time.unscaledTime;
        TargetJoinLobby = new CSteamID(lobbyId);
        LastJoinSuccess = null;
        LastJoinMessage = "参加中...";

        Debug.Log("[LobbyTest] JoinLobbyById: " + TargetJoinLobby);
        SteamMatchmaking.JoinLobby(TargetJoinLobby);
    }

    public void LeaveLobby()
    {
        // ★追加：Join中ならJoin中も解除
        IsJoining = false;

        if (!IsInLobby) return;

        SteamMatchmaking.LeaveLobby(currentLobby);
        currentLobby = default;
        Debug.Log("[LobbyTest] Left lobby");

        OnLobbyLeftEvent?.Invoke();
    }

    public List<CSteamID> GetLobbyMembers()
    {
        var list = new List<CSteamID>();
        if (!IsInLobby) return list;

        int n = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < n; i++)
            list.Add(SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i));

        return list;
    }

    private void OnLobbyCreated(LobbyCreated_t data)
    {
        if (data.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[LobbyTest] CreateLobby failed: " + data.m_eResult);
            return;
        }

        currentLobby = new CSteamID(data.m_ulSteamIDLobby);
        Debug.Log("[LobbyTest] Lobby Created: " + currentLobby);

        SteamMatchmaking.SetLobbyData(currentLobby, "host", SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobby, "name", SteamFriends.GetPersonaName());
        SteamMatchmaking.SetLobbyData(currentLobby, "ver", Application.version);

        // Host は作成＝参加済み扱いなので join状態はクリア
        IsJoining = false;
        TargetJoinLobby = default;
        LastJoinSuccess = true;
        LastJoinMessage = "ロビー作成成功";

        OnLobbyEnteredEvent?.Invoke(currentLobby);
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.Log("[LobbyTest] JoinRequested. Lobby: " + data.m_steamIDLobby);
        JoinLobbyById(data.m_steamIDLobby.m_SteamID); // ★Join処理を共通化
    }

    private void OnLobbyEntered(LobbyEnter_t data)
    {
        var lobbyId = new CSteamID(data.m_ulSteamIDLobby);
        var resp = (EChatRoomEnterResponse)data.m_EChatRoomEnterResponse;

        // ★LobbyEnterが来た時点で Join待ちは終了（成功/失敗に関わらず）
        IsJoining = false;
        TargetJoinLobby = default;

        if (resp == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            currentLobby = lobbyId;
            Debug.Log("[LobbyTest] Lobby Entered SUCCESS: " + currentLobby);

            LastJoinSuccess = true;
            LastJoinMessage = "参加成功";

            OnJoinResultEvent?.Invoke(true, LastJoinMessage, currentLobby);
            OnLobbyEnteredEvent?.Invoke(currentLobby);
        }
        else
        {
            currentLobby = default;

            LastJoinSuccess = false;
            LastJoinMessage = $"参加失敗: {resp}";

            Debug.LogWarning("[LobbyTest] Lobby Entered FAILED: " + LastJoinMessage);

            OnJoinResultEvent?.Invoke(false, LastJoinMessage, lobbyId);
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t data)
    {
        if (currentLobby.m_SteamID != data.m_ulSteamIDLobby) return;

        var userChanged = new CSteamID(data.m_ulSteamIDUserChanged);
        var state = (EChatMemberStateChange)data.m_rgfChatMemberStateChange;
        Debug.Log($"[LobbyTest] ChatUpdate: {userChanged} state={state}");
    }
}
