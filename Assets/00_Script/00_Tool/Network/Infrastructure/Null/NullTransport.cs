using GL.Network.Application.Ports;
using GL.Network.Domain;
using System;
using static GL.Network.Application.Ports.ITransport;

namespace GL.Network.Infrastructure.Null
{
    public sealed class NullTransport : ITransport
    {
        public void Send(PlayerId to, NetEnvelope env, SendReliability reliability)
        {
            // 何もしない（ダミー処理）
        }

        public int Receive(NetReceived[] buffer) => 0;
    }

    public sealed class NullPump : INetworkPump
    {
        public bool IsReady => true;
        public void Tick() { }
        public void Shutdown() { }
    }
}
