using System.Collections;
using System.Collections.Generic;
using TMPro;
using UNIHper;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace NetIO
{
    public class DIButton : MonoBehaviour
    {
        static Dictionary<IODIEnum, string> keyNames = new Dictionary<IODIEnum, string>()
        {
            { IODIEnum.CtrlMode, "控制模式" },
            { IODIEnum.Home, "主页" },
            { IODIEnum.Menu, "菜单" },
            { IODIEnum.Up, "上" },
            { IODIEnum.Down, "下" },
            { IODIEnum.Left, "左" },
            { IODIEnum.Right, "右" },
            { IODIEnum.Confirm, "确认" },
            { IODIEnum.Back, "返回" },
            { IODIEnum.VolUp, "音量+" },
            { IODIEnum.VolDown, "音量-" },
            { IODIEnum.Mute, "静音" },
            { IODIEnum.Play, "播放" },
            { IODIEnum.Pause, "暂停" },
            { IODIEnum.Replay, "重播" },
            { IODIEnum.Stop, "停止" },
            { IODIEnum.SysCtrl, "系统控制" },
        };

        public void Init(IODIEnum channel, string defaultName = "保留")
        {
            var _keyName = keyNames.GetValueOrDefault(channel, defaultName);
            this.Get<TextMeshProUGUI>("key_name").text = _keyName;
            // 将通道转成00x
            this.Get<TextMeshProUGUI>("key_channel").text = ((int)channel).ToString("000");
            bool isOn = false;
            var _netIOConfig = Managements.Config.Get<NetIOConfig>();
            this.Get("key_button")
                .AddComponent<ObservablePointerClickTrigger>()
                .OnPointerClickAsObservable()
                .Subscribe(_ =>
                {
                    isOn = !isOn;
                    this.Get<Image>("key_button").color = isOn ? Color.green : Color.white;
                    if (isOn)
                    {
                        _netIOConfig.SetRemoteKeyDown(channel);
                    }
                    else
                    {
                        _netIOConfig.SetRemoteKeyUp(channel);
                    }
                });
        }

        // Start is called before the first frame update
        void Start() { }

        // Update is called once per frame
        void Update() { }
    }
}
