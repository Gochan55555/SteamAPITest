using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if ONLINE_STEAM
using Steamworks;
#endif

namespace GL.Network.Presentation
{
    public sealed class LobbyBrowserUI : MonoBehaviour
    {
        [Header("Refs (optional)")]
        [SerializeField] private OnlineBootstrapMono boot;

        [Header("Search")]
        [SerializeField] private string nameContains = "";
        [SerializeField] private int maxResults = 30;

        [Header("UI")]
        [SerializeField] private bool showUI = true;

        private readonly List<LobbyEntry> _results = new();
        private Vector2 _scroll;

#if ONLINE_STEAM
        private CallResult<LobbyMatchList_t> _lobbyListCall;
        private CallResult<LobbyEnter_t> _lobbyEnterCall;
#endif

        private void Awake()
        {
            if (boot == null) boot = FindFirstObjectByType<OnlineBootstrapMono>();
#if ONLINE_STEAM
            _lobbyListCall = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
            _lobbyEnterCall = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
#endif
        }

        private void OnGUI()
        {
            if (!showUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 520, 720), GUI.skin.box);
            GUILayout.Label("Lobby Browser");

#if ONLINE_STEAM
            DrawSteamUI();
#else
            GUILayout.Label("ONLINE_STEAM が無効です（Steamロビー検索は使えません）");
#endif

            GUILayout.EndArea();
        }

#if ONLINE_STEAM
        private void DrawSteamUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name contains", GUILayout.Width(120));
            nameContains = GUILayout.TextField(nameContains, GUILayout.Width(240));
            if (GUILayout.Button("Search", GUILayout.Width(120)))
                RequestLobbyList();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (GUILayout.Button("Create Lobby (Public)"))
                TryCreateLobby();

            GUILayout.Space(10);
            GUILayout.Label($"Results: {_results.Count}");

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(560));

            for (int i = 0; i < _results.Count; i++)
            {
                var e = _results[i];

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{e.Name}  [{e.Members}/{(e.Capacity > 0 ? e.Capacity.ToString() : "?")}]");
                GUILayout.Label($"LobbyId: {e.LobbyId}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Join", GUILayout.Width(100)))
                    TryJoinLobby(e.LobbyId);
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void RequestLobbyList()
        {
            _results.Clear();

            // Steamworks.NETのバージョン差で int/uint のどちらのシグネチャもあり得る
            int limit = Mathf.Clamp(maxResults, 1, 50);
            TryAddLobbyListResultCountFilter(limit);

            SteamAPICall_t h = SteamMatchmaking.RequestLobbyList();
            _lobbyListCall.Set(h);
        }

        private void OnLobbyList(LobbyMatchList_t cb, bool ioFailure)
        {
            if (ioFailure)
            {
                Debug.LogWarning("[LobbyBrowserUI] RequestLobbyList failed (ioFailure)");
                return;
            }

            _results.Clear();

            // m_nLobbiesMatching が int/uint どっちでもコンパイル通すために明示キャストだけにする
            int count = (int)cb.m_nLobbiesMatching;
            if (count < 0) count = 0;

            for (int i = 0; i < count; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

                string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, "name");
                if (string.IsNullOrEmpty(lobbyName)) lobbyName = "(no name)";

                if (!string.IsNullOrWhiteSpace(nameContains) &&
                    lobbyName.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int members = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

                // GetLobbyMemberLimit が int/uint どっちでもOKにする
                int cap = 0;
                try
                {
                    cap = (int)SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
                    if (cap < 0) cap = 0;
                }
                catch
                {
                    cap = 0;
                }

                _results.Add(new LobbyEntry
                {
                    LobbyId = lobbyId.m_SteamID,
                    Name = lobbyName,
                    Members = members,
                    Capacity = cap
                });
            }

            Debug.Log($"[LobbyBrowserUI] Lobby results: {_results.Count}");
        }

        private void TryJoinLobby(ulong lobbyIdUlong)
        {
            if (TryJoinViaFacadeLobby(lobbyIdUlong))
                return;

            var lobbyId = new CSteamID(lobbyIdUlong);
            SteamAPICall_t h = SteamMatchmaking.JoinLobby(lobbyId);
            _lobbyEnterCall.Set(h);
        }

        private void OnLobbyEnter(LobbyEnter_t cb, bool ioFailure)
        {
            if (ioFailure)
            {
                Debug.LogWarning("[LobbyBrowserUI] JoinLobby failed (ioFailure)");
                return;
            }

            var lobby = new CSteamID(cb.m_ulSteamIDLobby);
            Debug.Log($"[LobbyBrowserUI] Entered Lobby: {lobby.m_SteamID}");
        }

        private void TryCreateLobby()
        {
            if (TryCreateViaFacadeLobby())
                return;

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
        }

        // -----------------------------
        // Steamworks.NET バージョン差吸収
        // -----------------------------
        private static void TryAddLobbyListResultCountFilter(int limit)
        {
            // SteamMatchmaking.AddRequestLobbyListResultCountFilter(int)
            // SteamMatchmaking.AddRequestLobbyListResultCountFilter(uint)
            // の両方を試す（どっちでもコンパイル通る）
            var t = typeof(SteamMatchmaking);

            try
            {
                var mInt = t.GetMethod("AddRequestLobbyListResultCountFilter",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(int) },
                    modifiers: null);

                if (mInt != null)
                {
                    mInt.Invoke(null, new object[] { limit });
                    return;
                }

                var mUint = t.GetMethod("AddRequestLobbyListResultCountFilter",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(uint) },
                    modifiers: null);

                if (mUint != null)
                {
                    uint u = (uint)Mathf.Max(0, limit);
                    mUint.Invoke(null, new object[] { u });
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserUI] ResultCountFilter invoke failed: {ex.Message}");
            }

            // 無くても動くので握りつぶす
        }

        // -----------------------------
        // Facade側 Join/Create を反射で呼ぶ（存在するならそっち優先）
        // -----------------------------
        private bool TryJoinViaFacadeLobby(ulong lobbyId)
        {
            if (boot == null || boot.Facade == null || boot.Facade.Lobby == null)
                return false;

            object lobbySvc = boot.Facade.Lobby;
            Type t = lobbySvc.GetType();

            string[] names = { "JoinLobby", "Join", "JoinById", "JoinLobbyById" };

            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo m = t.GetMethod(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) continue;

                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(ulong))
                {
                    try
                    {
                        m.Invoke(lobbySvc, new object[] { lobbyId });
                        Debug.Log($"[LobbyBrowserUI] Joined via Facade.Lobby.{names[i]}({lobbyId})");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LobbyBrowserUI] Facade join invoke failed: {ex}");
                    }
                }
            }

            return false;
        }

        private bool TryCreateViaFacadeLobby()
        {
            if (boot == null || boot.Facade == null || boot.Facade.Lobby == null)
                return false;

            object lobbySvc = boot.Facade.Lobby;
            Type t = lobbySvc.GetType();

            string[] names = { "CreateLobby", "Create", "CreatePublicLobby" };

            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo m = t.GetMethod(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) continue;

                var ps = m.GetParameters();

                try
                {
                    if (ps.Length == 0)
                    {
                        m.Invoke(lobbySvc, Array.Empty<object>());
                        Debug.Log($"[LobbyBrowserUI] Created via Facade.Lobby.{names[i]}()");
                        return true;
                    }
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(int))
                    {
                        m.Invoke(lobbySvc, new object[] { 4 });
                        Debug.Log($"[LobbyBrowserUI] Created via Facade.Lobby.{names[i]}(4)");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyBrowserUI] Facade create invoke failed: {ex}");
                }
            }

            return false;
        }

        private sealed class LobbyEntry
        {
            public ulong LobbyId;
            public string Name;
            public int Members;
            public int Capacity;
        }
#endif
    }
}
