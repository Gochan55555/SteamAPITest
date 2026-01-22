using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LobbyTest : MonoBehaviour
{
    private Callback<LobbyCreated_t> cbLobbyCreated;
    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyEnter_t> cbLobbyEntered;
    private Callback<LobbyChatUpdate_t> cbLobbyChatUpdate;

    private CSteamID currentLobby;
    private Callback<LobbyChatMsg_t> cbLobbyChatMsg;
    public event Action<CSteamID, string> OnChatMessageEvent;

    public CSteamID CurrentLobby => currentLobby;
    public bool IsInLobby => currentLobby.m_SteamID != 0;

    public event Action<CSteamID> OnLobbyEnteredEvent;
    public event Action OnLobbyLeftEvent;

    // Joinの結果をUIへ返す
    public event Action<bool, string, CSteamID> OnJoinResultEvent;

    // 参加状態（UIが参照できる）
    public bool IsJoining { get; private set; }
    public CSteamID TargetJoinLobby { get; private set; }
    public string LastJoinMessage { get; private set; } = "";
    public bool? LastJoinSuccess { get; private set; } = null;   // null=未実行/未確定

    [Header("Join Timeout")]
    [SerializeField] private float joinTimeoutSec = 10f;
    private float joinStartTime;

    // ロビー内ニックネーム（Steamプロフィール名とは別）
    public string LocalDisplayName { get; private set; } = "Player";

    void Awake()
    {
        cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        cbLobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        cbLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        cbLobbyChatMsg = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMsg);
    }

    void Update()
    {
        // LobbyEnter_tが返ってこない時の保険（タイムアウト）
        if (IsJoining && (Time.unscaledTime - joinStartTime) > joinTimeoutSec)
        {
            IsJoining = false;
            LastJoinSuccess = false;
            LastJoinMessage = $"参加失敗: Timeout({joinTimeoutSec:0}s)";

            Debug.LogWarning("[LobbyTest] Join TIMEOUT. target=" + TargetJoinLobby);
            OnJoinResultEvent?.Invoke(false, LastJoinMessage, TargetJoinLobby);
        }
    }
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(200, 200, 220, 200), GUI.skin.box);
        GUILayout.Label("Temp Lobby UI");

        if (!SteamBootstrap.IsReady)
        {
            GUILayout.Label("Steam not ready");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("InLobby: " + IsInLobby);
        GUILayout.Label("LobbyID: " + (IsInLobby ? CurrentLobby.m_SteamID.ToString() : "-"));

        if (!IsInLobby)
        {
            if (GUILayout.Button("Create FriendsOnly Lobby", GUILayout.Height(32)))
                CreateFriendsOnlyLobby(4);
        }
        else
        {
            if (GUILayout.Button("Leave Lobby", GUILayout.Height(28)))
                LeaveLobby();
        }

        GUILayout.EndArea();
    }
    // ====== Nickname ======
    private void OnLobbyChatMsg(LobbyChatMsg_t data)
    {
        if (!IsInLobby) return;
        if (currentLobby.m_SteamID != data.m_ulSteamIDLobby) return;

        // 送信者
        CSteamID sender;
        EChatEntryType entryType;
        byte[] buffer = new byte[4096];

        int len = SteamMatchmaking.GetLobbyChatEntry(
            new CSteamID(data.m_ulSteamIDLobby),
            (int)data.m_iChatID,
            out sender,
            buffer,
            buffer.Length,
            out entryType
        );

        if (len <= 0) return;

        // byte[] -> string（UTF8）
        string text = Encoding.UTF8.GetString(buffer, 0, len);

        // 自分のnickを優先して表示したいならGetMemberDisplayName(sender)使える
        OnChatMessageEvent?.Invoke(sender, text);
    }

    /// <summary>
    /// ロビー内の表示名を設定（Join/Create前に呼ぶ）
    /// </summary>
    public void SetLocalDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = "Player";

        LocalDisplayName = name.Trim();

        // 参加中なら即反映
        if (IsInLobby)
            ApplyLocalNameToLobby();
    }

    /// <summary>
    /// 自分の LobbyMemberData に nick を入れる（ロビーごとの名前）
    /// </summary>
    private void ApplyLocalNameToLobby()
    {
        if (!IsInLobby) return;

        // 自分のメンバーデータへ保存
        SteamMatchmaking.SetLobbyMemberData(currentLobby, "nick", LocalDisplayName);
    }

    /// <summary>
    /// ロビー内の任意メンバーのnick（無ければSteam名 or ID）
    /// </summary>
    public string GetMemberDisplayName(CSteamID member)
    {
        if (IsInLobby)
        {
            string nick = SteamMatchmaking.GetLobbyMemberData(currentLobby, member, "nick");
            if (!string.IsNullOrEmpty(nick))
                return nick;
        }

        // フレンドならSteam名を取れる（非フレンドは空になることがある）
        string steamName = SteamFriends.GetFriendPersonaName(member);
        if (!string.IsNullOrEmpty(steamName))
            return steamName;

        return member.m_SteamID.ToString();
    }

    // ====== Lobby ops ======

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

        // 参加中状態へ
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

    // ====== Callbacks ======

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

        // Host は作成＝参加済み扱い
        IsJoining = false;
        TargetJoinLobby = default;
        LastJoinSuccess = true;
        LastJoinMessage = "ロビー作成成功";

        // ★作成後すぐnick反映
        ApplyLocalNameToLobby();

        OnLobbyEnteredEvent?.Invoke(currentLobby);
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.Log("[LobbyTest] JoinRequested. Lobby: " + data.m_steamIDLobby);
        JoinLobbyById(data.m_steamIDLobby.m_SteamID);
    }

    private void OnLobbyEntered(LobbyEnter_t data)
    {
        var lobbyId = new CSteamID(data.m_ulSteamIDLobby);
        var resp = (EChatRoomEnterResponse)data.m_EChatRoomEnterResponse;

        // LobbyEnterが来たらJoin待ちは終了
        IsJoining = false;
        TargetJoinLobby = default;

        if (resp == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            currentLobby = lobbyId;
            Debug.Log("[LobbyTest] Lobby Entered SUCCESS: " + currentLobby);

            LastJoinSuccess = true;
            LastJoinMessage = "参加成功";

            // ★参加成功したらnick反映
            ApplyLocalNameToLobby();

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
