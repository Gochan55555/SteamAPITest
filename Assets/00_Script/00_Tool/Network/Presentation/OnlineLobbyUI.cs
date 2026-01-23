using GL.Network.Domain;
using GL.Network.Presentation;
using UnityEngine;

namespace GL.Network.Presentation
{
    public sealed class OnlineLobbyUI : MonoBehaviour
    {
        [SerializeField] private OnlineBootstrapMono boot;

        private string _name = "Player";
        private string _manualLobbyId = "";
        private string _chat = "";
        private Vector2 _scroll;

        void OnGUI()
        {
            var f = boot != null ? boot.Facade : null;
            if (f == null || !f.Lobby.IsReady)
            {
                GUILayout.BeginArea(new Rect(10, 10, 420, 100), GUI.skin.box);
                GUILayout.Label("Network not ready");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"InLobby: {f.Lobby.IsInLobby}", GUILayout.Width(140));

            ulong lobbyId = f.Lobby.CurrentLobby.Value;
            GUILayout.Label($"LobbyId: {lobbyId}", GUILayout.Width(200));

            GUI.enabled = lobbyId != 0;
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
            {
                GUIUtility.systemCopyBuffer = lobbyId.ToString();
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(50));
            _name = GUILayout.TextField(_name, GUILayout.Width(220));
            if (GUILayout.Button("Apply", GUILayout.Width(80)))
                f.Lobby.SetLocalDisplayName(_name);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (!f.Lobby.IsInLobby)
            {
                if (GUILayout.Button("Create Lobby (FriendsOnly)", GUILayout.Height(32)))
                    f.Lobby.CreateFriendsOnly(4);

                GUILayout.Space(8);
                GUILayout.Label("Manual Join (LobbyId)");

                GUILayout.BeginHorizontal();
                _manualLobbyId = GUILayout.TextField(_manualLobbyId, GUILayout.Width(240));
                if (GUILayout.Button("Join", GUILayout.Width(80)))
                {
                    if (ulong.TryParse(_manualLobbyId.Trim(), out var id) && id != 0)
                        f.Lobby.Join(new LobbyId(id));
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
                if (GUILayout.Button("Browse Lobbies", GUILayout.Height(28)))
                {
                    f.Lobby.RequestLobbies(
                        list => Debug.Log($"Lobbies: {list.Count}"),
                        err => Debug.LogError(err)
                    );
                }
            }
            else
            {
                if (GUILayout.Button("Leave Lobby", GUILayout.Height(28)))
                    f.Lobby.Leave();
            }

            GUILayout.Space(12);
            GUILayout.Label("Chat");

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            foreach (var line in f.ChatLog) GUILayout.Label(line);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _chat = GUILayout.TextField(_chat, GUILayout.Height(28));
            if (GUILayout.Button("Send", GUILayout.Width(80), GUILayout.Height(28)))
            {
                var msg = _chat;
                _chat = "";
                f.SendLobbyChat(msg);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
