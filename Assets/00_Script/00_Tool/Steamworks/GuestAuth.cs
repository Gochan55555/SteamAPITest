using UnityEngine;
using Steamworks;

namespace Tool.Steamworks
{
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static bool IsReady { get; private set; }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                // Steamworks.NETÇÃêÑèßÅFSteamÇ™ãNìÆÇµÇƒÇÈÅïAppIdÇ™ê≥ÇµÇ¢ëOíÒ
                IsReady = SteamAPI.Init();
                if (!IsReady)
                {
                    Debug.LogError("[SteamBootstrap] SteamAPI.Init failed.");
                }
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError("[SteamBootstrap] Steamworks dll not found: " + e);
                IsReady = false;
            }
        }

        void Update()
        {
            if (IsReady)
                SteamAPI.RunCallbacks();
        }

        void OnDestroy()
        {
            if (IsReady)
            {
                SteamAPI.Shutdown();
                IsReady = false;
            }
        }
    }
}