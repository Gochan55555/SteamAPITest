using UnityEngine;
using Steamworks;

public class SteamBootstrap : MonoBehaviour
{
    public static bool IsReady { get; private set; } = false;   // ★追加

    private bool showSteamRequiredWindow = false;
    private string errorMessage = "Steamが起動していません。\nSteamクライアントからゲームを起動してください。";

    void Awake()
    {
        TryInitSteam();
    }

    void TryInitSteam()
    {
        try
        {
            if (!SteamAPI.Init())
            {
                Debug.LogError("SteamAPI.Init() failed. Steamが起動していない可能性あり。");
                showSteamRequiredWindow = true;
                IsReady = false; // ★追加
                return;
            }

            IsReady = true;      // ★追加
            showSteamRequiredWindow = false;

            Debug.Log("Steam 初期化成功！");
            Debug.Log("Steam Name: " + SteamFriends.GetPersonaName());
            Debug.Log("SteamID: " + SteamUser.GetSteamID());
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("steam_api64.dll が見つかりません: " + e);
            errorMessage = "SteamのDLLが見つかりません。\nインストールまたは配置を確認してください。";
            showSteamRequiredWindow = true;
            IsReady = false; // ★追加
        }
    }

    void Update()
    {
        if (IsReady)
        {
            SteamAPI.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        if (IsReady)
        {
            SteamAPI.Shutdown();
            IsReady = false; // ★追加
        }
    }

    void OnGUI()
    {
        if (!showSteamRequiredWindow) return;

        float width = 400;
        float height = 180;

        Rect rect = new Rect(
            (Screen.width - width) / 2,
            (Screen.height - height) / 2,
            width,
            height
        );

        GUI.ModalWindow(0, rect, DrawSteamRequiredWindow, "Steamが必要です");
    }

    void DrawSteamRequiredWindow(int id)
    {
        GUILayout.Space(20);
        GUILayout.Label(errorMessage, GUILayout.ExpandHeight(true));

        GUILayout.Space(20);

        if (GUILayout.Button("終了", GUILayout.Height(30)))
        {
            Application.Quit();
        }

        GUI.DragWindow();
    }
}
