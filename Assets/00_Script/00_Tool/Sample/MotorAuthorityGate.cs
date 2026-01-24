using UnityEngine;

namespace GL.Network.Presentation
{
    /// <summary>
    /// 依存逆転のためのグルー:
    /// Motor側が Sync の authority を参照して、自分をON/OFFする。
    ///
    /// - NetworkTransformSync（や他のNetworkReplicatedBehaviour派生）を参照
    /// - LocalならMotor有効 / RemoteならMotor無効
    /// </summary>
    [RequireComponent(typeof(WASDCharacterMotor))]
    public sealed class MotorAuthorityGate : MonoBehaviour
    {
        [SerializeField] private NetworkReplicatedBehaviour sync;

        private WASDCharacterMotor _motor;

        void Awake()
        {
            _motor = GetComponent<WASDCharacterMotor>();
            if (sync == null) sync = GetComponent<NetworkReplicatedBehaviour>();
        }

        void Start() => Apply();

        void OnEnable() => Apply();

        private void Apply()
        {
            if (_motor == null) return;

            // Syncが無いなら、とりあえず有効（オフライン動作もできる）
            if (sync == null)
            {
                _motor.enabled = true;
                return;
            }

            _motor.enabled = sync.IsLocalAuthority;
        }
    }
}
