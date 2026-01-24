using System;
using System.Collections.Generic;
using UnityEngine;
using GL.Network.Domain;
using GL.Network.Application;

namespace GL.Network.Presentation
{
    /// <summary>
    /// ネットワーク同期の共通ベース（入力・移動などのゲームロジックは一切持たない）
    /// - Local: SerializeState() で状態を詰め、一定Hzで送信
    /// - Remote: 受信して ownerId をキーに対象を解決し、DeserializeAndApplyState() へ渡す
    /// </summary>
    public abstract class NetworkReplicatedBehaviour : MonoBehaviour
    {
        // enumに無くてもキャストで通す前提
        [Header("Network Kind")]
        [SerializeField] private byte kind = 200;

        [Header("Refs")]
        [SerializeField] private OnlineBootstrapMono boot;

        [Header("Ownership")]
        [Tooltip("自分が操作/送信するならON。受信して表示するだけならOFF。")]
        [SerializeField] private bool isLocalPlayer = true;

        [Tooltip("このオブジェクトのOwnerId(ulong)。SteamならSteamID。")]
        [SerializeField] private ulong ownerId;

        [Header("Network Send")]
        [Tooltip("送信先PlayerId(ulong)を手動指定（まずはここでテスト）。")]
        [SerializeField] private ulong[] sendTargets;

        [Tooltip("送信レート(Hz)")]
        [SerializeField] private float sendRateHz = 20f;

        [Header("Remote Spawn")]
        [Tooltip("受信対象が存在しない時にスポーンするPrefab（同じ派生クラス推奨 / isLocalPlayer=false）。")]
        [SerializeField] private NetworkReplicatedBehaviour remotePrefab;

        // ---- runtime ----
        protected OnlineFacade Facade { get; private set; }
        private float _sendAccum;

        // ownerId -> instance
        private static readonly Dictionary<ulong, NetworkReplicatedBehaviour> _byOwner = new();

        /// <summary>派生/他コンポーネントが参照できる authority（依存逆転用）</summary>
        public bool IsLocalAuthority => isLocalPlayer;

        /// <summary>この同期対象のオーナーID</summary>
        public ulong OwnerId => ownerId;

        protected virtual void Awake()
        {
            if (boot == null) boot = FindFirstObjectByType<OnlineBootstrapMono>();
            Facade = boot != null ? boot.Facade : null;

            if (Facade != null)
                Facade.OnPacket += OnPacket;

#if ONLINE_STEAM
            // ownerIdが未設定なら「自分のキャラ」だけ推定（Steam環境のみ）
            if (ownerId == 0 && isLocalPlayer)
            {
                try { ownerId = Steamworks.SteamUser.GetSteamID().m_SteamID; }
                catch { ownerId = 0; }
            }
#endif

            Register();
            OnNetInitialized();
        }

        protected virtual void OnDestroy()
        {
            if (Facade != null)
                Facade.OnPacket -= OnPacket;

            Unregister();
        }

        protected virtual void Update()
        {
            if (Facade == null || Facade.Lobby == null) return;
            if (!Facade.Lobby.IsInLobby) return;

            if (isLocalPlayer)
                TickSend();
        }

        // =========================
        // 派生が実装するポイント
        // =========================

        /// <summary>
        /// Local側：送信したい状態をpayloadへシリアライズ（ownerIdはベースで付与済み）
        /// </summary>
        protected abstract void SerializeState(List<byte> payload);

        /// <summary>
        /// Remote側：受信payload（ownerId除く状態部分）をデシリアライズして適用
        /// </summary>
        protected abstract void DeserializeAndApplyState(ReadOnlySpan<byte> payload);

        /// <summary>
        /// 初期化後に呼ばれる（派生側で初期スナップ等したい時用）
        /// </summary>
        protected virtual void OnNetInitialized() { }

        // =========================
        // Send
        // =========================
        private void TickSend()
        {
            if (sendTargets == null || sendTargets.Length == 0) return;
            if (sendRateHz <= 0f) return;

            _sendAccum += Time.deltaTime;
            float interval = 1f / sendRateHz;
            if (_sendAccum < interval) return;
            _sendAccum -= interval;

            // payload: [ownerId:8][customState:...]
            var buf = new List<byte>(128);
            WriteU64(buf, ownerId);
            SerializeState(buf);

            var envKind = (MessageKind)kind;

            for (int i = 0; i < sendTargets.Length; i++)
            {
                ulong to = sendTargets[i];
                if (to == 0) continue;

                Facade.SendPacket(
                    new PlayerId(to),
                    envKind,
                    buf.ToArray(),
                    SendReliability.Unreliable
                );
            }
        }

        // =========================
        // Receive
        // =========================
        private void OnPacket(PlayerId from, NetEnvelope env)
        {
            if (env.Kind != (MessageKind)kind) return;

            var span = env.Payload.Span;
            if (span.Length < 8) return;

            int o = 0;
            ulong remoteOwner = ReadU64(span, ref o);

            // 自分のLocalは無視
            if (remoteOwner == ownerId && isLocalPlayer) return;

            var target = GetOrSpawnRemote(remoteOwner);
            if (target == null) return;

            target.DeserializeAndApplyState(span.Slice(o));
        }

        // =========================
        // Spawn / Registry
        // =========================
        private void Register()
        {
            if (ownerId == 0) return;
            _byOwner[ownerId] = this;
        }

        private void Unregister()
        {
            if (ownerId == 0) return;
            if (_byOwner.TryGetValue(ownerId, out var cur) && cur == this)
                _byOwner.Remove(ownerId);
        }

        private NetworkReplicatedBehaviour GetOrSpawnRemote(ulong remoteOwner)
        {
            if (remoteOwner == 0) return null;

            if (_byOwner.TryGetValue(remoteOwner, out var exists) && exists != null)
                return exists;

            if (remotePrefab == null)
                return null;

            // Spawn位置は「Prefab側に任せる」でもOKだが、最低限現在Transform付近に出す
            var inst = Instantiate(remotePrefab, transform.position, transform.rotation);
            inst.isLocalPlayer = false;
            inst.ownerId = remoteOwner;

            // リモートは送信しない
            inst.sendTargets = Array.Empty<ulong>();

            inst.Register();
            inst.OnNetInitialized();
            return inst;
        }

        // =========================
        // Byte helpers
        // =========================
        protected static void WriteU64(List<byte> b, ulong v)
        {
            b.Add((byte)(v));
            b.Add((byte)(v >> 8));
            b.Add((byte)(v >> 16));
            b.Add((byte)(v >> 24));
            b.Add((byte)(v >> 32));
            b.Add((byte)(v >> 40));
            b.Add((byte)(v >> 48));
            b.Add((byte)(v >> 56));
        }

        protected static ulong ReadU64(ReadOnlySpan<byte> b, ref int o)
        {
            ulong v =
                ((ulong)b[o++]) |
                ((ulong)b[o++] << 8) |
                ((ulong)b[o++] << 16) |
                ((ulong)b[o++] << 24) |
                ((ulong)b[o++] << 32) |
                ((ulong)b[o++] << 40) |
                ((ulong)b[o++] << 48) |
                ((ulong)b[o++] << 56);
            return v;
        }

        protected static void WriteF32(List<byte> b, float v)
        {
            var bytes = BitConverter.GetBytes(v);
            b.AddRange(bytes);
        }

        protected static float ReadF32(ReadOnlySpan<byte> b, ref int o)
        {
            float v = BitConverter.ToSingle(b.Slice(o, 4));
            o += 4;
            return v;
        }
    }
}
