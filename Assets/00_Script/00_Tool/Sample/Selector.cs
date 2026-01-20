using UnityEngine;
using UnityEngine.InputSystem;

public class Selector : MonoBehaviour
{
    [SerializeField] GameObject loby;
    [SerializeField] GameObject guest;
    string debugDraw = "";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            loby.SetActive(true);
            guest.SetActive(false);
            debugDraw = "Loby Mode";
        }
        else if (Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            loby.SetActive(false);
            guest.SetActive(true);
            debugDraw = "Guest Mode";
        }
    }
    void OnGUI()
    {
        Color old = GUI.color;

        GUI.color = Color.red;  // Ç±Ç±Ç≈êFéwíË
        GUI.Label(new Rect(20, 20, 300, 30), debugDraw);

        GUI.color = old;
    }
}
