#if ONLINE_STEAM
using System;
using System.Runtime.InteropServices;
using GL.Network.Application.Ports;
using GL.Network.Domain;
using Steamworks;

namespace GL.Network.Infrastructure.Steam
{
    public sealed class SteamTransport : ITransport
    {
        private const int Channel = 0;

        // NOTE: Ç±Ç±ÇÕ SteamNetworkingSendType / flags Ç…çáÇÌÇπÇƒè´óàí≤êÆ
        private const int SEND_RELIABLE = 8;

        public void Send(PlayerId to, NetEnvelope env, SendReliability reliability)
        {
            var id = new SteamNetworkingIdentity();
            id.SetSteamID(new CSteamID(to.Value));

            // [kind:1][seq:2][tick:4][len:2][payload]
            var payload = env.Payload.ToArray();
            ushort len = (ushort)payload.Length;

            byte[] raw = new byte[1 + 2 + 4 + 2 + len];
            int o = 0;
            raw[o++] = (byte)env.Kind;
            WriteU16(raw, ref o, env.Seq);
            WriteU32(raw, ref o, env.Tick);
            WriteU16(raw, ref o, len);
            Buffer.BlockCopy(payload, 0, raw, o, len);

            IntPtr p = Marshal.AllocHGlobal(raw.Length);
            try
            {
                Marshal.Copy(raw, 0, p, raw.Length);

                // Ç¢Ç‹ÇÕ Reliable å≈íËÅiUnreliableÇÕ sendType Çï™äÚÇ≥ÇπÇƒägí£Åj
                int sendType = SEND_RELIABLE;

                SteamNetworkingMessages.SendMessageToUser(ref id, p, (uint)raw.Length, sendType, Channel);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        public int Receive(ITransport.NetReceived[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0;

            int max = Math.Min(buffer.Length, 64);
            IntPtr[] msgs = new IntPtr[max];

            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, msgs, max);
            int outCount = 0;

            for (int i = 0; i < n && outCount < buffer.Length; i++)
            {
                var msg = SteamNetworkingMessage_t.FromIntPtr(msgs[i]);
                try
                {
                    int size = (int)msg.m_cbSize;
                    if (size < 1 + 2 + 4 + 2) continue;

                    byte[] raw = new byte[size];
                    Marshal.Copy(msg.m_pData, raw, 0, size);

                    int o = 0;
                    var kind = (MessageKind)raw[o++];
                    ushort seq = ReadU16(raw, ref o);
                    uint tick = ReadU32(raw, ref o);
                    ushort len = ReadU16(raw, ref o);

                    if (o + len > raw.Length) continue;

                    byte[] payload = new byte[len];
                    Buffer.BlockCopy(raw, o, payload, 0, len);

                    var from = msg.m_identityPeer.GetSteamID();

                    buffer[outCount++] = new ITransport.NetReceived(
                        new PlayerId(from.m_SteamID),
                        new NetEnvelope(kind, seq, tick, payload)
                    );
                }
                finally
                {
                    msg.Release();
                }
            }

            return outCount;
        }

        private static void WriteU16(byte[] b, ref int o, ushort v)
        {
            b[o++] = (byte)(v & 0xFF);
            b[o++] = (byte)((v >> 8) & 0xFF);
        }

        private static void WriteU32(byte[] b, ref int o, uint v)
        {
            b[o++] = (byte)(v & 0xFF);
            b[o++] = (byte)((v >> 8) & 0xFF);
            b[o++] = (byte)((v >> 16) & 0xFF);
            b[o++] = (byte)((v >> 24) & 0xFF);
        }

        private static ushort ReadU16(byte[] b, ref int o)
            => (ushort)(b[o++] | (b[o++] << 8));

        private static uint ReadU32(byte[] b, ref int o)
            => (uint)(b[o++] | (b[o++] << 8) | (b[o++] << 16) | (b[o++] << 24));
    }
}
#endif
