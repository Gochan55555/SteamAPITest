// Assets/00_Script/00_Tool/Network/Application/OnlineFacade.cs

using System.Collections.Generic;
using System.Text;
using GL.Network.Application.Ports;
using GL.Network.Domain;

namespace GL.Network.Application
{
    public sealed class OnlineFacade
    {
        private readonly ILobbyService _lobby;
        private readonly ITransport _transport;
        private readonly IChatService _chat;

        private readonly ITransport.NetReceived[] _recvBuf = new ITransport.NetReceived[64];

        private ushort _seq;
        private uint _tick;

        public readonly List<string> ChatLog = new();

        public OnlineFacade(ILobbyService lobby, ITransport transport, IChatService chat)
        {
            _lobby = lobby;
            _transport = transport;
            _chat = chat;

            _lobby.OnEntered += _ => AddSys("Lobby Entered");
            _lobby.OnLeft += () => AddSys("Lobby Left");

            if (_chat != null)
            {
                _chat.OnMessage += (from, text) =>
                {
                    var name = _lobby.GetMemberDisplayName(from);
                    AddLine($"{name}: {text}");
                };
            }
        }

        public ILobbyService Lobby => _lobby;

        public void Tick()
        {
            _tick++;

            int n = _transport.Receive(_recvBuf);
            for (int i = 0; i < n; i++)
            {
                var r = _recvBuf[i];

                // 例：P2Pで Chat を流したいならここで処理
                if (r.Envelope.Kind == MessageKind.Chat)
                {
                    string text = Encoding.UTF8.GetString(r.Envelope.Payload.Span);
                    string name = _lobby.GetMemberDisplayName(r.From);
                    AddLine($"{name}: {text}");
                }
                // Snapshot/InputCommand/Rpc はここで分岐してゲーム側へ
            }
        }

        public void SendLobbyChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _chat?.Send(text);

            // ※即時反映が欲しいならここを有効化。
            // ただしSteam側が自分の発言も返す環境だと二重表示になる。
            // AddLine($"Me: {text}");
        }

        public void SendPacket(PlayerId to, MessageKind kind, byte[] payload, SendReliability reliability)
        {
            if (payload == null) payload = System.Array.Empty<byte>();
            var env = new NetEnvelope(kind, _seq++, _tick, payload);
            _transport.Send(to, env, reliability);
        }

        private void AddSys(string s) => AddLine("[SYS] " + s);

        private void AddLine(string s)
        {
            ChatLog.Add(s);
            if (ChatLog.Count > 200)
                ChatLog.RemoveRange(0, ChatLog.Count - 200);
        }
    }
}
