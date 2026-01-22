using UnityEngine;
using UnityEngine.InputSystem;

public class Selector : MonoBehaviour
{
    [SerializeField] GameObject loby;
    [SerializeField] GameObject guest;

    [SerializeField] private bool allowKeyboardShortcut = true;

    string debugDraw = "";

    void Update()
    {
        if (!allowKeyboardShortcut) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.numpad1Key.wasPressedThisFrame) SetLobbyMode();
        else if (Keyboard.current.numpad2Key.wasPressedThisFrame) SetGuestMode();
    }

    void OnGUI()
    {
        const float w = 220f;
        const float h = 130f;
        const float margin = 10f;

        // âEè„Ç…îzíu
        var rect = new Rect(Screen.width - w - margin, margin, w, h);

        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Mode Select");

        if (GUILayout.Button("Lobby Mode", GUILayout.Height(30)))
            SetLobbyMode();

        if (GUILayout.Button("Guest Mode", GUILayout.Height(30)))
            SetGuestMode();

        GUILayout.Label(debugDraw);
        GUILayout.EndArea();
    }

    private void SetLobbyMode()
    {
        if (loby != null) loby.SetActive(true);
        if (guest != null) guest.SetActive(false);
        debugDraw = "Lobby Mode";
    }

    private void SetGuestMode()
    {
        Debug.Log("[Selector] SetGuestMode()");
        if (loby != null) loby.SetActive(false);
        if (guest != null) guest.SetActive(true);
        debugDraw = "Guest Mode";
    }
}
