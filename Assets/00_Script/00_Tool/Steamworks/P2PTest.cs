using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Steamworks;

public class P2PTest : MonoBehaviour
{
    [SerializeField] private LobbyTest lobby;

    private const int Channel = 0;
    private const int SEND_RELIABLE = 8;

    private bool inLobby;
    private CSteamID hostId;

    // Chat state
    private readonly List<string> chatLog = new();
    private string chatInput = "";
    private Vector2 chatScroll;

    // 旧FriendSelectUI互換（残っててもコンパイル通す用）
    private CSteamID currentTarget;

    void Update()
    {
        RefreshLobbyState();
        ReceiveLoop();
    }

    private void RefreshLobbyState()
    {
        if (lobby == null) return;

        var lob = lobby.CurrentLobby;

        if (!inLobby && lob.m_SteamID != 0)
        {
            // hostデータがあればそれを使う。無ければownerをホスト扱いにする
            var hostStr = SteamMatchmaking.GetLobbyData(lob, "host");
            if (ulong.TryParse(hostStr, out var hostU))
                hostId = new CSteamID(hostU);
            else
                hostId = SteamMatchmaking.GetLobbyOwner(lob);

            inLobby = true;
            AddSystem("チャット準備OK / Host=" + hostId);
        }

        if (inLobby && lob.m_SteamID == 0)
        {
            inLobby = false;
            hostId = default;
            AddSystem("ロビー退出：チャット停止");
        }
    }

    // ★SteamLobbyUIから呼ぶ（P2PTestが直接OnGUIしない）
    public void DrawChatUI(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Room Chat");

        if (!inLobby || lobby == null || !lobby.IsInLobby)
        {
            GUILayout.Label("ロビー未参加（参加するとチャットできます）");
            GUILayout.EndArea();
            return;
        }

        bool iAmHost = (SteamUser.GetSteamID() == hostId);
        GUILayout.Label(iAmHost ? "あなたはホスト" : "あなたは参加者");

        chatScroll = GUILayout.BeginScrollView(chatScroll, GUILayout.Height(260));
        foreach (var line in chatLog) GUILayout.Label(line);
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        chatInput = GUILayout.TextField(chatInput, GUILayout.Height(28));
        if (GUILayout.Button("Send", GUILayout.Width(80), GUILayout.Height(28)))
        {
            var msg = chatInput;
            chatInput = "";
            SendChatToLobby(msg);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // =========================
    // Chat Send
    // =========================
    public void SendChatToLobby(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (lobby == null || !lobby.IsInLobby)
        {
            AddSystem("ロビー未参加なので送れません");
            return;
        }

        var me = SteamUser.GetSteamID();
        var name = lobby.LocalDisplayName; // ★ロビーnick

        // 自分の画面にも表示
        chatLog.Add($"{name}: {text}");
        TrimLog();

        // ロビーメンバー全員へ（自分以外）
        var members = lobby.GetLobbyMembers();
        foreach (var m in members)
        {
            if (m == me) continue;

            // CHAT|本文（送信者名は受信側でfromから引く。改ざんしにくい）
            SendTo(m, $"CHAT|{text}");
        }
    }

    // =========================
    // Ping (optional)
    // =========================
    private void SendPingToHost()
    {
        if (!inLobby || hostId.m_SteamID == 0)
        {
            AddSystem("Hostが不明なのでPingできません");
            return;
        }
        SendTo(hostId, "PING from " + SteamFriends.GetPersonaName());
    }

    // 旧FriendSelectUI互換
    public void SetTarget(CSteamID id)
    {
        currentTarget = id;
        AddSystem("[P2P] Target set: " + id);
    }
    public void SendPing()
    {
        if (currentTarget.m_SteamID == 0)
        {
            AddSystem("[P2P] No target selected");
            return;
        }
        SendTo(currentTarget, "PING from " + SteamFriends.GetPersonaName());
    }

    // =========================
    // Low-level Send/Recv
    // =========================
    private void SendTo(CSteamID to, string text)
    {
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

            if (result != EResult.k_EResultOK)
                Debug.LogWarning($"[P2P] Send failed result={result} to={to} text={text}");
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
        }
    }

    private void ReceiveLoop()
    {
        const int max = 16;
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

                if (text.StartsWith("CHAT|"))
                {
                    // CHAT|body
                    string body = text.Length > 5 ? text.Substring(5) : "";

                    // ★送信者名は from の LobbyMemberData(nick) を優先
                    string fromName = (lobby != null) ? lobby.GetMemberDisplayName(from) : from.m_SteamID.ToString();

                    chatLog.Add($"{fromName}: {body}");
                    TrimLog();
                }
                else
                {
                    chatLog.Add($"[RECV] from={from} text={text}");
                    TrimLog();
                }
            }
            finally
            {
                msg.Release();
            }
        }
    }

    private void AddSystem(string text)
    {
        chatLog.Add("[SYS] " + text);
        TrimLog();
    }

    private void TrimLog()
    {
        const int maxLines = 200;
        if (chatLog.Count > maxLines)
            chatLog.RemoveRange(0, chatLog.Count - maxLines);
    }
}
