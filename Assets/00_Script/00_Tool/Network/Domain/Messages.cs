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

    public enum ChatLineState
    { 
        Pending, 
        Confirmed, 
        Failed 
    }

    public readonly struct ChatMessage
    {
        public readonly string MessageId;   // server id
        public readonly string SenderId;    // user id
        public readonly string SenderName;
        public readonly long UnixMs;
        public readonly string Text;

        public ChatMessage(string messageId, string senderId, string senderName, long unixMs, string text)
        {
            MessageId = messageId;
            SenderId = senderId;
            SenderName = senderName;
            UnixMs = unixMs;
            Text = text;
        }
    }

    public sealed class ChatLine
    {
        public string LocalTempId;      // client temp id
        public string ServerMessageId;  // server id
        public string SenderName;
        public string Text;
        public ChatLineState State;
        public long UnixMs;
    }
}
