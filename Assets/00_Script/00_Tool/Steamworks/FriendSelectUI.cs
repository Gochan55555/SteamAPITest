using UnityEngine;
using Steamworks;
using System.Collections.Generic;

public class FriendSelectUI : MonoBehaviour
{
    [SerializeField] private P2PTest p2p;

    private Vector2 scroll;
    private List<CSteamID> friends = new();
    private CSteamID selected;

    void OnEnable()
    {
        Refresh();
    }

    void Refresh()
    {
        friends = SteamFriendUtil.GetOnlineFriends();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(50000, 10, 320, 400), GUI.skin.box);
        GUILayout.Label("Send Target (Friends)");

        if (GUILayout.Button("Refresh", GUILayout.Height(25)))
            Refresh();

        scroll = GUILayout.BeginScrollView(scroll, false, true);

        foreach (var f in friends)
        {
            string name = SteamFriends.GetFriendPersonaName(f);
            bool isSel = f == selected;

            GUI.backgroundColor = isSel ? Color.green : Color.white;
            if (GUILayout.Button(name, GUILayout.Height(28)))
            {
                selected = f;
                p2p.SetTarget(f);
            }
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label("Selected:");
        GUILayout.Label(selected.m_SteamID != 0
            ? SteamFriends.GetFriendPersonaName(selected)
            : "None");

        GUILayout.EndArea();
    }
}
