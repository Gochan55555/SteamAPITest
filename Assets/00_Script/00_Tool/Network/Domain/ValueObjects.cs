namespace GL.Network.Domain
{
    public readonly struct PlayerId
    {
        public readonly ulong Value;
        public PlayerId(ulong value) => Value = value;
        public bool IsValid => Value != 0;
        public override string ToString() => Value.ToString();
    }

    public readonly struct LobbyId
    {
        public readonly ulong Value;
        public LobbyId(ulong value) => Value = value;
        public bool IsValid => Value != 0;
        public override string ToString() => Value.ToString();
    }
}
