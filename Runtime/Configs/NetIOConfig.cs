using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UNIHper;
using System.Xml.Serialization;

using UniRx;
using System;
using UnityEngine.Events;
using System.Linq;

namespace NetIO
{
    public enum IODIEnum
    {
        CtrlMode = 0,
        Home = 1,
        Menu = 2,
        Up = 3,
        Down = 4,
        Left = 5,
        Right = 6,
        Confirm = 7,
        Back = 8,
        VolUp = 9,
        VolDown = 10,
        Mute = 11,
        DI_12 = 12,
        DI_13 = 13,
        DI_14 = 14,
        DI_15 = 15,
        Play = 16,
        Pause = 17,
        Replay = 18,
        Stop = 19,
        DI_20,
        DI_21,
        DI_22,
        DI_23,
        DI_24,
        DI_25,
        DI_26,
        DI_27,
        DI_28,
        DI_29,
        DI_30,
        DI_31,

        SysCtrl = 250
    }

    public enum IOADEnum
    {
        PanDeltaX = 0,
        PanDeltaY = 1,
        ScaleFactor = 2,
        Rotate1 = 3,

        Volume = 11
    }

    public class NetIONode
    {
        [XmlAttribute]
        public string IP;

        [XmlAttribute]
        public int Port;

        [XmlAttribute]
        public string PrcessName = string.Empty;

        [XmlAttribute]
        public string Key = string.Empty;

        [XmlIgnore]
        private System.DateTime _lastHeartbeat;

        [XmlIgnore]
        public float HeartbeatTimeout { get; set; } = 7;

        [XmlIgnore]
        public System.DateTime LastBeatTime
        {
            get { return _lastHeartbeat; }
            set
            {
                _lastHeartbeat = value;
                IsAlive.Value = true;
            }
        }

        private ReactiveProperty<bool> IsAlive = new ReactiveProperty<bool>(false);

        public IObservable<bool> OnAliveChangeAsObservable() => IsAlive.DistinctUntilChanged();

        public NetIONode()
        {
            LastBeatTime = System.DateTime.Now;
            Observable
                .Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    IsAlive.Value =
                        (System.DateTime.Now - LastBeatTime)
                        < TimeSpan.FromSeconds(HeartbeatTimeout);
                })
                .AddTo(UNIHperEntry.Instance);
        }

        [XmlIgnore]
        public string NameText =>
            $"<color={(this.IsAlive.Value ? "#FF0000" : "#05C84C")}>\u25CF</color> <color=#05C84C>{PrcessName.TruncateWithEllipsis(16)}</color>  ({IP}:{Port})";
    }

#if UNITY_EDITOR
    [SerializedAt(AppPath.StreamingDir)]
#endif
    public class NetIOConfig : UConfig
    {
        public List<NetIONode> Nodes = new List<NetIONode>();

        [XmlIgnore]
        public ReactiveProperty<NetIONode> remoteNode = new ReactiveProperty<NetIONode>(
            new NetIONode { IP = "127.0.0.1", Port = 20001 }
        );

        public void EnableSystemCtrl()
        {
            SetRemoteKeyDown(IODIEnum.SysCtrl);
        }

        public void DisableSystemCtrl()
        {
            SetRemoteKeyUp(IODIEnum.SysCtrl);
        }

        public void EmmitHomeEvent()
        {
            SetRemoteKeyDown(IODIEnum.Home);
            SetRemoteKeyUp(IODIEnum.Home);
        }

        public void EmmitNetEvent(IODIEnum channel)
        {
            SetRemoteKeyDown(channel);
            SetRemoteKeyUp(channel);
        }

        public void SetRemoteNode(int nodeID)
        {
            SetRemoteNode(this.Nodes[nodeID]);
        }

        public void SetRemoteNode(NetIONode node)
        {
            this.remoteNode.Value = node;
            Debug.Log(
                $"Set remote node: {remoteNode.Value.PrcessName}, IP:{remoteNode.Value.IP}, Port:{remoteNode.Value.Port}"
            );
        }

        public void SetRemoteKeyDown(IODIEnum channel)
        {
            SendNetIOMessage(NetIOMessage.BuildKeyDownEvent(channel));
        }

        public void EnterControlMode()
        {
            SendNetIOMessage(NetIOMessage.BuildKeyDownEvent(IODIEnum.CtrlMode));
        }

        public void LeaveControlMode()
        {
            SendNetIOMessage(NetIOMessage.BuildKeyUpEvent(IODIEnum.CtrlMode));
        }

        public void SendNetIOMessage(NetIOMessage msg)
        {
            if (remoteNode is null)
            {
                Debug.LogWarning("Remote node is not set yet.");
                return;
            }
            Managements.Network.Send2UdpClient(
                msg.ToJson().ToUTF8Bytes(),
                remoteNode.Value.IP,
                remoteNode.Value.Port,
                localIPKey
            );
        }

        public void SetRemoteKeyUp(IODIEnum channel)
        {
            SendNetIOMessage(NetIOMessage.BuildKeyUpEvent(channel));
        }

        public void SetRemoteAxis(IOADEnum axis, float value)
        {
            SendNetIOMessage(NetIOMessage.BuildADEvent(axis, value * 1000));
        }

        public void SetRemoteAxis(Dictionary<string, string> data)
        {
            SendNetIOMessage(NetIOMessage.BuildADEvent(data));
        }

        private UnityEvent<int, NetIONode> onNewNode = new UnityEvent<int, NetIONode>();
        private UnityEvent<int, NetIONode> onUpdateNode = new UnityEvent<int, NetIONode>();

        public IObservable<Tuple<int, NetIONode>> OnNewNodeAsObservable()
        {
            return onNewNode.AsObservable();
        }

        public IObservable<Tuple<int, NetIONode>> OnUpdateNodeAsObservable()
        {
            return onUpdateNode.AsObservable();
        }

        public NetIONode AddOrGetNetIONode(string key)
        {
            var node = Nodes.Find(n => n.Key == key);
            if (node == null)
            {
                node = new NetIONode()
                {
                    IP = "127.0.0.1",
                    Port = 20001,
                    PrcessName = "unknown",
                    Key = key
                };
                Nodes.Add(node);
                this.Save();
                onNewNode.Invoke(Nodes.Count - 1, node);
            }
            return node;
        }

        public NetIONode AddOrUpdateNetIONode(NetIOMessage msg)
        {
            var node = Nodes.Find(n => n.IP == msg.RemoteIP && n.Port == msg.RemotePort);
            if (node == null)
            {
                node = new NetIONode()
                {
                    IP = msg.RemoteIP,
                    Port = msg.RemotePort,
                    PrcessName = msg.Data.GetValueOrDefault("process_name", "unknown")
                };
                Nodes.Add(node);
                this.Save();
                onNewNode.Invoke(Nodes.Count - 1, node);
            }
            else
            {
                node.PrcessName = msg.Data.GetValueOrDefault("process_name", "unknown");
                node.LastBeatTime = System.DateTime.Now;
                onUpdateNode.Invoke(Nodes.IndexOf(node), node);
            }
            return node;
        }

        // Write your comments here
        protected override string Comment()
        {
            return @"
        Write your comments here...
        ";
        }

        const string localIP = "0.0.0.0";

        [XmlAttribute("port")]
        int localPort = 19000; // NetIO服务端口
        string localIPKey => $"{localIP}_{localPort}";
        const int HeartbeatPort = 21000; // 心跳端口
        string heartbeatIPKey => $"{localIP}_{HeartbeatPort}";

        // Called once after the config data is loaded
        protected override void OnLoaded()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            MulticastLock();
#endif
            Managements.Network
                .BuildUdpListener("0.0.0.0", HeartbeatPort, new NetIOMsgReceiver())
                .EnableBroadcast()
                .Listen()
                .OnReceivedAsObservable()
                .Subscribe(_ =>
                {
                    var _netIOMsg = _.Message as NetIOMessage;
                    AddOrUpdateNetIONode(_netIOMsg);
                    Debug.LogWarning(
                        $"Received message from {_netIOMsg.RemoteKey} {_netIOMsg.Evt} {_netIOMsg.Msg}"
                    );
                });

            // 使用22000广播端口，主动探测在线设备 (解决安卓设备中无法接受广播消息的问题)
            Observable
                .Interval(TimeSpan.FromSeconds(5))
                .Subscribe(_ =>
                {
                    Debug.Log("Send heartbeat");
                    Managements.Network.SendUdpBroadcast(
                        NetIOMessage.BuildHeartbeatEvent().ToJson().ToUTF8Bytes(),
                        22000,
                        heartbeatIPKey
                    );
                })
                .AddTo(UNIHperEntry.Instance);

            // 用于向NetIO发送数据的服务
            Managements.Network
                .BuildUdpListener(localIP, localPort, new NetIOMsgReceiver())
                .EnableBroadcast()
                .Listen()
                .OnReceivedAsObservable()
                .Subscribe(_ =>
                {
                    var _msg = _.Message as NetIOMessage;
                    Debug.Log(_msg);
                });

            // 改变目标设备时的逻辑控制
            remoteNode
                .Pairwise()
                .Subscribe(pair =>
                {
                    var _old = pair.Previous;
                    var _new = pair.Current;

                    if (_old != null)
                    {
                        LeaveControlMode();
                    }
                    SetRemoteNode(_new);
                    // 进入控制模式
                    EnterControlMode();
                    // 启用系统控制
                    EnableSystemCtrl();
                });

            onNewNode
                .AsObservable()
                .Do(_ => listenNodeAliveStatus())
                .Subscribe()
                .AddTo(UNIHperEntry.Instance);
        }

        IDisposable nodeAliveDisposable;

        private void listenNodeAliveStatus()
        {
            nodeAliveDisposable?.Dispose();
            nodeAliveDisposable = Nodes
                .Select(
                    _node =>
                        _node
                            .OnAliveChangeAsObservable()
                            .Select(_alive => (NetIONode: _node, IsAlive: _alive))
                )
                .Merge()
                .Subscribe(evt =>
                {
                    Debug.Log(
                        $"Node {evt.NetIONode.PrcessName} is {(evt.IsAlive ? "alive" : "dead")}"
                    );
                    onNodeAliveChanged.Invoke(evt.NetIONode, evt.IsAlive);
                });
        }

        private UnityEvent<NetIONode, bool> onNodeAliveChanged = new UnityEvent<NetIONode, bool>();

        public IObservable<Tuple<NetIONode, bool>> OnNodeAliveChangedAsObservable()
        {
            return onNodeAliveChanged.AsObservable();
        }

        protected override void OnUnloaded()
        {
            Managements.Network.Dispose();
#if UNITY_ANDROID && !UNITY_EDITOR
            multicastLock?.Call("release");
#endif
        }

        AndroidJavaObject multicastLock;

        void MulticastLock()
        {
            using (
                AndroidJavaObject activity = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer"
                ).GetStatic<AndroidJavaObject>("currentActivity")
            )
            {
                using (
                    var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi")
                )
                {
                    Debug.Log("Multicast lock");
                    multicastLock = wifiManager.Call<AndroidJavaObject>(
                        "createMulticastLock",
                        "lock"
                    );
                    multicastLock.Call("acquire");
                    Debug.Log("Multicast lock acquired");
                }
            }
        }
    }
}
