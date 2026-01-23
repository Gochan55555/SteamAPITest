using System;

namespace GL.Network.Domain
{
    public enum MessageKind : byte
    {
        Chat = 1,

        // ActionŒü‚¯
        InputCommand = 10,
        Snapshot = 11,
        Rpc = 12,

        // ‰¹º‚Í g§Œäh ‚¾‚¯ MessageKind ‚ÉÚ‚¹‚é‚Ì‚ª–³“ï
        VoiceControl = 30,
    }

    public enum SendReliability
    {
        Reliable,
        Unreliable
    }

    public readonly struct NetEnvelope
    {
        public readonly MessageKind Kind;
        public readonly ushort Seq;
        public readonly uint Tick;
        public readonly ReadOnlyMemory<byte> Payload;

        public NetEnvelope(MessageKind kind, ushort seq, uint tick, ReadOnlyMemory<byte> payload)
        {
            Kind = kind;
            Seq = seq;
            Tick = tick;
            Payload = payload;
        }
    }
}
