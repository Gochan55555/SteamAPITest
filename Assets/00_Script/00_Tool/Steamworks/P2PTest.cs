using System;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;

public class P2PTest : MonoBehaviour
{
    [SerializeField] private LobbyTest lobby;

    private CSteamID hostId;
    private bool inLobby;

    private const int Channel = 0;
    private CSteamID currentTarget;

    // Steamworksの送信フラグ（steamnetworkingtypes.h）
    // Reliable = 8 :contentReference[oaicite:3]{index=3}
    private const int SEND_RELIABLE = 8;

    void Update()
    {
        if (!inLobby && lobby != null && lobby.GetCurrentLobby().m_SteamID != 0)
        {
            var lob = lobby.GetCurrentLobby();
            var hostStr = SteamMatchmaking.GetLobbyData(lob, "host");

            if (ulong.TryParse(hostStr, out var hostU))
            {
                hostId = new CSteamID(hostU);
                inLobby = true;
                Debug.Log("P2P ready. Host=" + hostId);
            }
        }

        ReceiveLoop();
    }

    void OnGUI()
    {
        if (!inLobby) return;

        GUILayout.BeginArea(new Rect(10, 220, 460, 200), GUI.skin.box);
        GUILayout.Label("P2P Test (ISteamNetworkingMessages)");

        bool iAmHost = (SteamUser.GetSteamID() == hostId);
        GUILayout.Label(iAmHost ? "あなたはホスト" : "あなたは参加者");

        if (!iAmHost)
        {
            if (GUILayout.Button("ホストへPING送信", GUILayout.Height(35)))
            {
                SendPing();
            }
        }
        else
        {
            GUILayout.Label("ホストは受信待ち（参加者からのPINGをログに表示）");
        }

        GUILayout.EndArea();
    }
    public void SendPing()
    {
        if (currentTarget.m_SteamID == 0)
        {
            Debug.LogError("[P2P] No target selected");
            return;
        }

        SendTo(currentTarget, "PING from " + SteamFriends.GetPersonaName());
    }
    public void SetTarget(CSteamID id)
    {
        currentTarget = id;
        Debug.Log("[P2P] Target set: " + id);
    }

    private void SendTo(CSteamID to, string text)
    {
        // CSteamID -> SteamNetworkingIdentity に変換 :contentReference[oaicite:4]{index=4}
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(to);

        // byte[] -> IntPtr（アンマネージメモリへコピー）
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        IntPtr pData = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pData, bytes.Length);

            // SendMessageToUser(identity, dataPtr, size, sendFlags, channel) :contentReference[oaicite:5]{index=5}
            var result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                pData,
                (uint)bytes.Length,
                SEND_RELIABLE,
                Channel
            );

            Debug.Log($"SendMessageToUser result={result} text={text}");
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

        int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, msgs, max); // :contentReference[oaicite:6]{index=6}
        for (int i = 0; i < n; i++)
        {
            // SteamNetworkingMessage_t を IntPtr から取得
            var msg = SteamNetworkingMessage_t.FromIntPtr(msgs[i]);
            try
            {
                int size = (int)msg.m_cbSize;
                byte[] buf = new byte[size];
                Marshal.Copy(msg.m_pData, buf, 0, size);

                string text = Encoding.UTF8.GetString(buf);

                // 相手のSteamID（Identityから取得）
                var from = msg.m_identityPeer.GetSteamID();
                Debug.Log($"[P2P RECV] from={from} text={text}");
            }
            finally
            {
                msg.Release(); // 受信したら必ず解放 :contentReference[oaicite:7]{index=7}
            }
        }
    }
}
