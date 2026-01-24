using System;
using System.Collections.Generic;
using UnityEngine;

namespace GL.Network.Presentation
{
    /// <summary>
    /// Transform同期（何を送るかは派生で決める、ここはTransform送信の具体例）
    /// - Local: 自分のTransformを送信
    /// - Remote: 受信したTransformを補間して表示
    ///
    /// 注意：このクラスは Motor を参照しない（依存逆転）。
    /// 入力/移動の有効化は MotorAuthorityGate 側が担当する。
    /// </summary>
    public sealed class NetworkTransformSync : NetworkReplicatedBehaviour
    {
        [Header("Remote Smoothing")]
        [SerializeField] private float lerpPos = 12f;
        [SerializeField] private float lerpRot = 16f;

        [Header("Warp")]
        [Tooltip("この距離以上ズレたら補間せず即座にワープ")]
        [SerializeField] private float warpDistance = 4f;

        private Vector3 _netPos;
        private Quaternion _netRot = Quaternion.identity;

        protected override void OnNetInitialized()
        {
            _netPos = transform.position;
            _netRot = transform.rotation;
        }

        protected override void SerializeState(List<byte> payload)
        {
            // [px,py,pz][qx,qy,qz,qw]
            WriteF32(payload, transform.position.x);
            WriteF32(payload, transform.position.y);
            WriteF32(payload, transform.position.z);

            WriteF32(payload, transform.rotation.x);
            WriteF32(payload, transform.rotation.y);
            WriteF32(payload, transform.rotation.z);
            WriteF32(payload, transform.rotation.w);
        }

        protected override void DeserializeAndApplyState(ReadOnlySpan<byte> payload)
        {
            // payloadは ownerId を除いた部分だけ来る
            if (payload.Length < (3 + 4) * 4) return;

            int o = 0;
            float px = ReadF32(payload, ref o);
            float py = ReadF32(payload, ref o);
            float pz = ReadF32(payload, ref o);

            float qx = ReadF32(payload, ref o);
            float qy = ReadF32(payload, ref o);
            float qz = ReadF32(payload, ref o);
            float qw = ReadF32(payload, ref o);

            _netPos = new Vector3(px, py, pz);
            _netRot = new Quaternion(qx, qy, qz, qw);
        }

        void Update()
        {
            base.Update(); // Local送信

            // Remoteだけ補間表示
            if (IsLocalAuthority) return;

            float dist = Vector3.Distance(transform.position, _netPos);
            if (dist > warpDistance)
            {
                transform.position = _netPos;
                transform.rotation = _netRot;
                return;
            }

            transform.position = Vector3.Lerp(
                transform.position,
                _netPos,
                1f - Mathf.Exp(-lerpPos * Time.deltaTime)
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _netRot,
                1f - Mathf.Exp(-lerpRot * Time.deltaTime)
            );
        }
    }
}
