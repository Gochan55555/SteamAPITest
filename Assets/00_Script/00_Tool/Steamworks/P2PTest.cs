using System;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;

public class P2PTestUI : MonoBehaviour
{
    [Header("Lobby")]
    [SerializeField] private LobbyTest lobby;

    private CSteamID hostId;
    private CSteamID myId;
    private bool inLobby;

    private const int Channel = 0;
    private const int SEND_RELIABLE = 8;

    private string sendText = "PING";
    private Vector2 logScroll;
    private readonly StringBuilder log = new StringBuilder(2048);

    private bool receiveEnabled = true;

    private enum TargetMode { Host, MySelf, CustomSteamId }
    private TargetMode targetMode = TargetMode.Host;
    private string customTargetSteamId = "";

    void Start()
    {
        myId = SteamUser.GetSteamID();
        AppendLog($"MySteamID={myId}");
    }

    void Update()
    {
        TryResolveLobbyInfo();

        if (inLobby && receiveEnabled)
            ReceiveLoop();
    }

    private void TryResolveLobbyInfo()
    {
        if (inLobby) return;
        if (lobby == null) return;

        var lob = lobby.GetCurrentLobby();
        if (lob.m_SteamID == 0) return;

        var hostStr = SteamMatchmaking.GetLobbyData(lob, "host");
        if (!ulong.TryParse(hostStr, out var hostU) || hostU == 0) return;

        hostId = new CSteamID(hostU);
        myId = SteamUser.GetSteamID();
        inLobby = true;

        AppendLog($"P2P ready. Host={hostId} / Me={myId}");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 520, 520), GUI.skin.box);
        GUILayout.Label("P2P Test UI (ISteamNetworkingMessages)");

        // SteamManager非依存チェック
        if (!SteamAPI.IsSteamRunning())
        {
            GUILayout.Label("Steamが起動していません");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Space(6);

        if (!inLobby)
        {
            GUILayout.Label("ロビー未参加 or host情報未取得");
            GUILayout.Label("（LobbyDataの 'host' が設定されると自動でP2P準備OKになります）");
            GUILayout.EndArea();
            return;
        }

        bool iAmHost = (myId == hostId);
        GUILayout.Label(iAmHost ? "あなたはホスト" : "あなたはゲスト");
        GUILayout.Label($"Host: {hostId}");
        GUILayout.Label($"Me  : {myId}");

        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        receiveEnabled = GUILayout.Toggle(receiveEnabled, " 受信を有効化");
        if (GUILayout.Button("ログクリア", GUILayout.Width(110)))
        {
            log.Length = 0;
            AppendLog("Log cleared.");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        GUILayout.Label("送信先:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(targetMode == TargetMode.Host, "ホスト", GUI.skin.button)) targetMode = TargetMode.Host;
        if (GUILayout.Toggle(targetMode == TargetMode.MySelf, "自分(ループバック)", GUI.skin.button)) targetMode = TargetMode.MySelf;
        if (GUILayout.Toggle(targetMode == TargetMode.CustomSteamId, "SteamID指定", GUI.skin.button)) targetMode = TargetMode.CustomSteamId;
        GUILayout.EndHorizontal();

        if (targetMode == TargetMode.CustomSteamId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SteamID:", GUILayout.Width(70));
            customTargetSteamId = GUILayout.TextField(customTargetSteamId, GUILayout.Width(260));
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);

        GUILayout.Label("送信テキスト:");
        sendText = GUILayout.TextField(sendText);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("送信", GUILayout.Height(35)))
        {
            var to = ResolveTarget();
            if (to.m_SteamID == 0)
                AppendLog("[SEND] Target invalid.");
            else
                SendTo(to, sendText);
        }

        if (GUILayout.Button("ホストへ送信", GUILayout.Height(35)))
        {
            SendTo(hostId, $"BROADCAST->HOST: {sendText}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("ログ:");
        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(250));
        GUILayout.TextArea(log.ToString());
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private CSteamID ResolveTarget()
    {
        switch (targetMode)
        {
            case TargetMode.Host:
                return hostId;
            case TargetMode.MySelf:
                return myId;
            case TargetMode.CustomSteamId:
                if (ulong.TryParse(customTargetSteamId, out var u) && u != 0)
                    return new CSteamID(u);
                return new CSteamID(0);
            default:
                return new CSteamID(0);
        }
    }

    private void SendTo(CSteamID to, string text)
    {
        if (to.m_SteamID == 0)
        {
            AppendLog("[SEND] to is zero");
            return;
        }

        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(to);

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        IntPtr pData = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, pData, bytes.Length);

            var result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                pData,
                (uint)bytes.Length,
                SEND_RELIABLE,
                Channel
            );

            AppendLog($"[SEND] to={to} result={result} text={text}");
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
        }
    }

    private void ReceiveLoop()
    {
        const int max = 32;
        IntPtr[] msgs = new IntPtr[max];

        int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, msgs, max);
        for (int i = 0; i < n; i++)
        {
            var msg = SteamNetworkingMessage_t.FromIntPtr(msgs[i]);
            try
            {
                int size = (int)msg.m_cbSize;
                byte[] buf = new byte[size];
                Marshal.Copy(msg.m_pData, buf, 0, size);

                string text = Encoding.UTF8.GetString(buf);
                var from = msg.m_identityPeer.GetSteamID();

                AppendLog($"[RECV] from={from} size={size} text={text}");
            }
            finally
            {
                msg.Release();
            }
        }
    }

    private void AppendLog(string s)
    {
        if (log.Length > 20000)
        {
            log.Remove(0, 10000);
            log.Insert(0, "(trimmed)\n");
        }
        log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {s}");
    }
}
