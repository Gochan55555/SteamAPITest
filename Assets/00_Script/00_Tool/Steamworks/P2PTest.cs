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

    private CSteamID hostId;
    private bool inLobby;

    // chat
    private readonly List<string> chatLog = new();
    private string chatInput = "";
    private Vector2 chatScroll;
    private CSteamID currentTarget;

    void Update()
    {
        RefreshLobbyState();
        ReceiveLoop();
    }

    private void RefreshLobbyState()
    {
        if (lobby == null) return;

        // ★GetCurrentLobby() ではなく CurrentLobby を使う
        var lob = lobby.CurrentLobby;

        if (!inLobby && lob.m_SteamID != 0)
        {
            var hostStr = SteamMatchmaking.GetLobbyData(lob, "host");
            if (ulong.TryParse(hostStr, out var hostU))
            {
                hostId = new CSteamID(hostU);
                inLobby = true;
                AddSystem("P2P ready. Host=" + hostId);
            }
            else
            {
                // hostが未設定でもロビー参加はできるので、ownerで代替
                hostId = SteamMatchmaking.GetLobbyOwner(lob);
                inLobby = true;
                AddSystem("P2P ready. Host(owner)=" + hostId);
            }
        }

        if (inLobby && lob.m_SteamID == 0)
        {
            inLobby = false;
            hostId = default;
            AddSystem("Left lobby. P2P stopped.");
        }
    }

    // SteamLobbyUI から呼ぶ用（UIはここに集約する）
    public void DrawChatUI(Rect rect)
    {
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Chat (P2P)");

        if (!inLobby || lobby == null || !lobby.IsInLobby)
        {
            GUILayout.Label("ロビー未参加");
            GUILayout.EndArea();
            return;
        }

        bool iAmHost = (SteamUser.GetSteamID() == hostId);
        GUILayout.Label(iAmHost ? "あなたはホスト" : "あなたは参加者");

        chatScroll = GUILayout.BeginScrollView(chatScroll, GUILayout.Height(240));
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

        GUILayout.BeginHorizontal();
        if (!iAmHost)
        {
            if (GUILayout.Button("Ping Host", GUILayout.Width(120), GUILayout.Height(26)))
            {
                SendPingToHost();
            }
        }
        else
        {
            GUILayout.Label("Host: 受信待ち");
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void SendPingToHost()
    {
        if (currentTarget.m_SteamID == 0)
        {
            AddSystem("[P2P] No target selected");
            return;
        }
        SendTo(hostId, "PING from " + SteamFriends.GetPersonaName());
    }

    public void SetTarget(CSteamID id)
    {
        currentTarget = id;
        AddSystem("[P2P] Target set: " + id);
    }
    public void SendChatToLobby(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (lobby == null || !lobby.IsInLobby)
        {
            AddSystem("ロビー未参加なので送れません");
            return;
        }

        var me = SteamUser.GetSteamID();
        var name = SteamFriends.GetPersonaName();

        // 自分のログ表示
        chatLog.Add($"{name}: {text}");
        TrimLog();

        // ロビー全員へ送信（自分以外）
        var members = lobby.GetLobbyMembers();
        foreach (var m in members)
        {
            if (m == me) continue;
            SendTo(m, $"CHAT|{name}|{text}");
        }
    }

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
                Debug.LogWarning($"[P2P] SendMessageToUser result={result} to={to} text={text}");
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
                    var parts = text.Split(new[] { '|' }, 3);
                    if (parts.Length == 3)
                    {
                        chatLog.Add($"{parts[1]}: {parts[2]}");
                        TrimLog();
                    }
                    else
                    {
                        chatLog.Add($"[CHAT?] from={from} text={text}");
                        TrimLog();
                    }
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
