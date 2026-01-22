using UnityEngine;

public class GuestJoinUI : MonoBehaviour
{
    [SerializeField] private LobbyTest lobby; // ★Inspectorで割り当て

    private string lobbyIdText = "";
    private string status = "";

    void OnGUI()
    {
        // guest画面にだけ表示したいなら、guestオブジェクト自体をActive/Inactiveしてる前提でOK
        GUILayout.BeginArea(new Rect(10, 10, 460, 180), GUI.skin.box);
        GUILayout.Label("Guest Join");

        GUILayout.Label("Lobby ID を入力して参加（ホスト側で表示/コピーしたID）");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Lobby ID:", GUILayout.Width(70));
        lobbyIdText = GUILayout.TextField(lobbyIdText, GUILayout.Width(300));
        GUILayout.EndHorizontal();

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("貼り付け", GUILayout.Height(28), GUILayout.Width(80)))
            {
                lobbyIdText = GUIUtility.systemCopyBuffer ?? "";
            }

            if (GUILayout.Button("参加", GUILayout.Height(28), GUILayout.Width(80)))
            {
                TryJoin();
            }
        }

        if (!string.IsNullOrEmpty(status))
            GUILayout.Label(status);

        GUILayout.EndArea();
    }

    private void TryJoin()
    {
        if (lobby == null)
        {
            status = "LobbyTest が未設定です（Inspectorで割り当てて）";
            return;
        }

        if (!ulong.TryParse(lobbyIdText.Trim(), out var id) || id == 0)
        {
            status = "Lobby ID が不正です";
            return;
        }

        status = "参加リクエスト送信: " + id;
        lobby.JoinLobbyById(id);
    }
}
