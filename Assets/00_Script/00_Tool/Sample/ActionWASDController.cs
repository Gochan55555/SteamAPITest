using UnityEngine;
using UnityEngine.InputSystem;
namespace GL.Network.Presentation
{
    /// <summary>
    /// WASD入力で移動するだけ（ネットワークは一切しない）
    /// </summary>
    public sealed class WASDCharacterMotor : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Turn")]
        [SerializeField] private float turnSpeedDeg = 540f;

        [Header("Options")]
        [Tooltip("カメラ基準移動にしたい場合ON")]
        [SerializeField] private bool moveRelativeToCamera = false;

        private Vector3 _vel;
        public Vector3 Velocity => _vel;

        void Update()
        {
            // --- New Input System ---
            Vector2 move = ReadMoveVector();   // (-1..1, -1..1)

            Vector3 input = new Vector3(move.x, 0f, move.y);
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 desired = input;

            if (moveRelativeToCamera && Camera.main != null)
            {
                var cam = Camera.main.transform;

                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                fwd.Normalize();

                Vector3 right = cam.right;
                right.y = 0f;
                right.Normalize();

                desired = right * input.x + fwd * input.z;
                desired = Vector3.ClampMagnitude(desired, 1f);
            }

            _vel = desired * moveSpeed;

            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.SimpleMove(_vel);
            }
            else
            {
                transform.position += _vel * Time.deltaTime;
            }

            if (desired.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(desired, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    turnSpeedDeg * Time.deltaTime
                );
            }
        }

        private static Vector2 ReadMoveVector()
        {
            Vector2 v = Vector2.zero;

            // キーボードWASD
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed) v.x += 1f;
                if (kb.sKey.isPressed) v.y -= 1f;
                if (kb.wKey.isPressed) v.y += 1f;
            }

            // ゲームパッド左スティックも足す（任意）
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                // キーボード優先したいなら stick を加算じゃなく最大値にする等も可能
                v += stick;
            }

            // 正規化（斜めが速くならないように）
            v = Vector2.ClampMagnitude(v, 1f);
            return v;
        }
    }
}