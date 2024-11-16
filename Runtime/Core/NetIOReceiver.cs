using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DNHper;
using Newtonsoft.Json;
using UNIHper.Network;

namespace NetIO
{
    public class NetIOMessage : UMessage
    {
        private static int ID_COUNTER = 0;

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("evt")]
        public string Evt { get; set; }

        [JsonProperty("msg")]
        public string Msg { get; set; } = "";

        [JsonProperty("data")]
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();

        public NetIOMessage()
        {
            Id = ID_COUNTER++ % 1000000;
        }

        public static NetIOMessage BuildKeyDownEvent(IODIEnum channel)
        {
            return new NetIOMessage
            {
                Evt = "SetDI",
                Data = new Dictionary<string, string> { { ((int)channel).ToString(), "1" } }
            };
        }

        public static NetIOMessage BuildKeyUpEvent(IODIEnum channel)
        {
            return new NetIOMessage
            {
                Evt = "SetDI",
                Data = new Dictionary<string, string> { { ((int)channel).ToString(), "0" } }
            };
        }

        public static NetIOMessage BuildADEvent(IOADEnum axis, float value)
        {
            return new NetIOMessage
            {
                Evt = "SetAD",
                Data = new Dictionary<string, string>
                {
                    { ((int)axis).ToString(), value.ToString() }
                }
            };
        }

        public static NetIOMessage BuildADEvent(Dictionary<string, string> Data)
        {
            return new NetIOMessage { Evt = "SetAD", Data = Data };
        }

        public static NetIOMessage BuildHeartbeatEvent()
        {
            return new NetIOMessage { Evt = "Heartbeat" };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override string ToString()
        {
            return ToJson();
        }
    }

    public class NetIOMsgReceiver : UNetMsgReceiver
    {
        public int ReadBufferSize = 4096;
        CancellationTokenSource recvHandler = null;

        public override void OnConnected()
        {
            if (protocol == NetProtocol.Tcp)
            {
                startTcpReceiver();
            }
            else if (protocol == NetProtocol.Udp)
            {
                startUdpReceiver();
            }
        }

        private void startTcpReceiver()
        {
            if (recvHandler != null)
            {
                recvHandler.Cancel();
                recvHandler = null;
            }
            recvHandler = new CancellationTokenSource();
            byte[] _buffer = new byte[ReadBufferSize];
            Task.Factory.StartNew(
                () =>
                {
                    while (!recvHandler.Token.IsCancellationRequested)
                    {
                        try
                        {
                            bool _connected =
                                !socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
                            if (!_connected)
                            {
                                recvHandler.Cancel();
                                fireDisconnectedEvent();
                                return;
                            }
                            Array.Clear(_buffer, 0, _buffer.Length);
                            int _received = socket.Receive(_buffer);
                            pushMessage(
                                new NetStringMessage
                                {
                                    Content = Encoding.UTF8
                                        .GetString(_buffer.Slice(0, _received))
                                        .Trim(),
                                    RawData = _buffer.Slice(0, _received)
                                }
                            );
                        }
                        catch (System.Exception)
                        {
                            //UnityEngine.Debug.LogWarning (e.Message);
                        }
                    }
                },
                recvHandler.Token
            );
        }

        private void startUdpReceiver()
        {
            if (recvHandler != null)
            {
                recvHandler.Cancel();
                recvHandler = null;
            }
            recvHandler = new CancellationTokenSource();
            byte[] _buffer = new byte[ReadBufferSize];
            Task.Factory.StartNew(
                () =>
                {
                    while (!recvHandler.Token.IsCancellationRequested)
                    {
                        // UnityEngine.Debug.Log("------------");
                        try
                        {
                            Array.Clear(_buffer, 0, _buffer.Length);
                            EndPoint _endPoint = new IPEndPoint(IPAddress.Any, 0);
                            int _received = socket.ReceiveFrom(_buffer, ref _endPoint);
                            IPEndPoint _ipEP = _endPoint as IPEndPoint;
                            RemoteIP = _ipEP.Address.ToString();
                            RemotePort = _ipEP.Port;

                            var _validBuf = _buffer.Slice(0, _received);
                            var _msg = Encoding.UTF8.GetString(_validBuf).Trim();
                            var _netIOEvent = JsonConvert.DeserializeObject<NetIOMessage>(_msg);
                            _netIOEvent.RawData = _validBuf;

                            pushMessage(_netIOEvent);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning(e.Message);
                        }
                    }
                    // UnityEngine.Debug.Log($"{socket.LocalEndPoint} receiver disposed");
                },
                recvHandler.Token
            );
        }

        public override void Dispose()
        {
            base.Dispose();
            if (recvHandler != null)
            {
                recvHandler.Cancel();
                recvHandler = null;
            }
        }
    }
}
