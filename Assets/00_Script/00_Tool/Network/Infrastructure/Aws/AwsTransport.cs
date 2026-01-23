#if ONLINE_AWS
using System;
using GL.Network.Application.Ports;
using GL.Network.Domain;

namespace GL.Network.Infrastructure.Aws
{
    // ‚±‚±‚Í«—ˆ WebSocket/UDP/Relay ‚É·‚µ‘Ö‚¦
    public sealed class AwsTransport : ITransport
    {
        public void Send(PlayerId to, NetEnvelope env, SendReliability reliability)
        {
            // TODO: ŽÀ‘•iWebSocket/UDP“™j
        }

        public int Receive(ITransport.NetReceived[] buffer)
        {
            // TODO: ŽÀ‘•
            return 0;
        }
    }

    public sealed class AwsPump : INetworkPump
    {
        public bool IsReady => true;
        public void Tick() { /* TODO: WebSocket pump ‚È‚Ç */ }
        public void Shutdown() { }
    }
}
#endif
