using GL.Network.Application;
using GL.Network.Application.Ports;

namespace GL.Network.Application
{
    // ˆË‘¶‚ð‚Ð‚Æ‚Ü‚Æ‚ßiBootstrap—pj
    public sealed class OnlineRuntime
    {
        public readonly INetworkPump Pump;
        public readonly OnlineFacade Facade;

        public OnlineRuntime(INetworkPump pump, OnlineFacade facade)
        {
            Pump = pump;
            Facade = facade;
        }

        public bool IsReady => Pump != null && Pump.IsReady;

        public void Tick()
        {
            Pump?.Tick();
            Facade?.Tick();
        }

        public void Shutdown() => Pump?.Shutdown();
    }
}
