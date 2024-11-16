using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UNIHper;
using UNIHper.UI;
using DNHper;
using TMPro;
using System;
using DigitalRubyShared;

using UniRx.Triggers;
using NetIO;
using System.Linq;

[UIPage(Asset = "NetIOControlUI", Order = -1, Type = UIType.Normal)]
public class NetIOControlUI : UIBase
{
    // Start is called before the first frame update
    private void Start() { }

    // Update is called once per frame
    private void Update() { }

    private void bindKeyEvent(Transform transform, IODIEnum channel)
    {
        var _netIOConfig = Managements.Config.Get<NetIOConfig>();
        transform
            .AddComponent<ObservablePointerDownTrigger>()
            .OnPointerDownAsObservable()
            .Subscribe(_ =>
            {
                _netIOConfig.SetRemoteKeyDown(channel);
                if (channel == IODIEnum.Up)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaY, 1);
                }
                else if (channel == IODIEnum.Down)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaY, -1);
                }
                else if (channel == IODIEnum.Left)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaX, -1);
                }
                else if (channel == IODIEnum.Right)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaX, 1);
                }
            });

        transform
            .AddComponent<ObservablePointerUpTrigger>()
            .OnPointerUpAsObservable()
            .Subscribe(_ =>
            {
                _netIOConfig.SetRemoteKeyUp(channel);
                if (channel == IODIEnum.Up || channel == IODIEnum.Down)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaY, 0);
                }
                else if (channel == IODIEnum.Left || channel == IODIEnum.Right)
                {
                    _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaX, 0);
                }
            });
    }

    // Called when this ui is loaded
    protected override void OnLoaded()
    {
        this.Get("di_fixed_panel/channels").Children().ForEach(_ => DestroyImmediate(_.gameObject));
        this.Get("di_custom_panel/channels")
            .Children()
            .ForEach(_ => DestroyImmediate(_.gameObject));
        var _diButton = Managements.Resource.Get<GameObject>("DI_Button");

        Enumerable
            .Range(0, 16)
            .ForEach(_idx =>
            {
                var _di = (IODIEnum)_idx;
                var _diButtonObj = Instantiate(
                    _diButton,
                    this.Get("di_fixed_panel/channels").transform
                );
                _diButtonObj.name = _di.ToString();
                _diButtonObj.Get<DIButton>().Init(_di, "保留");
            });

        Enumerable
            .Range(16, 16)
            .ForEach(_idx =>
            {
                var _di = (IODIEnum)_idx;
                var _diButtonObj = Instantiate(
                    _diButton,
                    this.Get("di_custom_panel/channels").transform
                );
                _diButtonObj.name = _di.ToString();
                _diButtonObj.Get<DIButton>().Init(_di, "自定义");
            });

        var _netIOConfig = Managements.Config.Get<NetIOConfig>();

        var _dropdown = this.Get<TMP_Dropdown>("Dropdown");
        _dropdown.AddOptions(_netIOConfig.Nodes.ConvertAll(n => n.NameText));

        _netIOConfig
            .OnNewNodeAsObservable()
            .Subscribe(_ =>
            {
                var _last = _dropdown.value;
                _dropdown.ClearOptions();
                _dropdown.AddOptions(_netIOConfig.Nodes.ConvertAll(n => n.NameText));
                _dropdown.value = _last;
            });

        _dropdown.onValueChanged
            .AsObservable()
            .Subscribe(_ =>
            {
                _netIOConfig.SetRemoteNode(_);
            });

        _netIOConfig
            .OnNodeAliveChangedAsObservable()
            .Subscribe(_ =>
            {
                var _item = _.Item1;
                var _idx = _netIOConfig.Nodes.IndexOf(_item);
                _dropdown.options[_idx].text = _item.NameText;
                _dropdown.RefreshShownValue();
            });

        var _touchPad = this.Get("touch_pad").gameObject;

        bindKeyEvent(_touchPad.transform.Get("top"), IODIEnum.Up);
        bindKeyEvent(_touchPad.transform.Get("down"), IODIEnum.Down);
        bindKeyEvent(_touchPad.transform.Get("left"), IODIEnum.Left);
        bindKeyEvent(_touchPad.transform.Get("right"), IODIEnum.Right);

        PanGestureRecognizer panGesture = new PanGestureRecognizer();
        panGesture.PlatformSpecificView = _touchPad;
        panGesture.StateUpdated += gesture =>
        {
            if (panGesture.State == GestureRecognizerState.Executing)
            {
                var _deltaX = panGesture.DeltaX;
                var _deltaY = panGesture.DeltaY;
                _netIOConfig.SetRemoteAxis(
                    new Dictionary<string, string>
                    {
                        { ((int)IOADEnum.PanDeltaX).ToString(), _deltaX.ToString() },
                        { ((int)IOADEnum.PanDeltaY).ToString(), _deltaY.ToString() }
                    }
                );
            }
            else if (panGesture.State == GestureRecognizerState.Ended)
            {
                _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaX, 0);
                _netIOConfig.SetRemoteAxis(IOADEnum.PanDeltaY, 0);
            }
        };

        FingersScript.Instance.AddGesture(panGesture);

        var _scaleGesture = new ScaleGestureRecognizer();
        _scaleGesture.MinimumNumberOfTouchesToTrack = 2;
        _scaleGesture.PlatformSpecificView = _touchPad;
        _scaleGesture.StateUpdated += gesture =>
        {
            if (_scaleGesture.State == GestureRecognizerState.Executing)
            {
                var _scale = _scaleGesture.ScaleMultiplier;
                if (_scale >= 0.999f && _scale <= 1.001f)
                {
                    return;
                }
                _netIOConfig.SetRemoteAxis(IOADEnum.ScaleFactor, _scale);
            }
            else if (_scaleGesture.State == GestureRecognizerState.Ended)
            {
                _netIOConfig.SetRemoteAxis(IOADEnum.ScaleFactor, 1);
            }
        };
        FingersScript.Instance.AddGesture(_scaleGesture);

        var _rotateGesture = new RotateGestureRecognizer();
        _rotateGesture.MaximumNumberOfTouchesToTrack = 2;
        _rotateGesture.PlatformSpecificView = _touchPad;
        _rotateGesture.StateUpdated += gesture =>
        {
            if (_rotateGesture.State == GestureRecognizerState.Executing)
            {
                var _rotation = _rotateGesture.RotationDegreesDelta;
                _netIOConfig.SetRemoteAxis(IOADEnum.Rotate1, _rotation);
                Debug.Log($"rotation: {_rotation}");
            }
            else if (_rotateGesture.State == GestureRecognizerState.Ended)
            {
                _netIOConfig.SetRemoteAxis(IOADEnum.Rotate1, 0);
            }
        };
        FingersScript.Instance.AddGesture(_rotateGesture);

        var swipeGesture = new SwipeGestureRecognizer();
        swipeGesture.DirectionThreshold = 0;
        swipeGesture.MinimumNumberOfTouchesToTrack = 1;
        swipeGesture.PlatformSpecificView = _touchPad;
        swipeGesture.StateUpdated += gesture =>
        {
            if (gesture.State == GestureRecognizerState.Ended)
            {
                var _dir = swipeGesture.EndDirection;
                Debug.LogWarning($"swipe: {_dir}");
                if (_dir == SwipeGestureRecognizerDirection.Left)
                {
                    _netIOConfig.SetRemoteKeyDown(IODIEnum.Left);
                    _netIOConfig.SetRemoteKeyUp(IODIEnum.Left);
                }
                else if (_dir == SwipeGestureRecognizerDirection.Right)
                {
                    _netIOConfig.SetRemoteKeyDown(IODIEnum.Right);
                    _netIOConfig.SetRemoteKeyUp(IODIEnum.Right);
                }
                else if (_dir == SwipeGestureRecognizerDirection.Up)
                {
                    _netIOConfig.SetRemoteKeyDown(IODIEnum.Up);
                    _netIOConfig.SetRemoteKeyUp(IODIEnum.Up);
                }
                else if (_dir == SwipeGestureRecognizerDirection.Down)
                {
                    _netIOConfig.SetRemoteKeyDown(IODIEnum.Down);
                    _netIOConfig.SetRemoteKeyUp(IODIEnum.Down);
                }
            }
        };
        swipeGesture.AllowSimultaneousExecutionWithAllGestures();
        FingersScript.Instance.AddGesture(swipeGesture);

        this.Get<Toggle>("toggle_showTouch")
            .OnValueChangedAsObservable()
            .Subscribe(_ =>
            {
                FingersScript.Instance.ShowTouches = _;
            });

        var _volumeText = this.Get<TextMeshProUGUI>("volume_slider/volume");

        this.Get<Slider>("volume_slider")
            .OnValueChangedAsObservable()
            .Subscribe(_val =>
            {
                _netIOConfig.SetRemoteAxis(IOADEnum.Volume, _val);
                _volumeText.text = Mathf.FloorToInt(_val * 100).ToString();
            });

        this.Get<Toggle>("volume_slider/icon")
            .OnValueChangedAsObservable()
            .Subscribe(_mute =>
            {
                if (_mute)
                {
                    _netIOConfig.SetRemoteKeyDown(IODIEnum.Mute);
                }
                else
                {
                    _netIOConfig.SetRemoteKeyUp(IODIEnum.Mute);
                }
            });
    }

    // Called when this ui is shown
    protected override void OnShown()
    {
        // FingersScript.Instance.ShowTouches = true;
    }

    // Called when this ui is hidden
    protected override void OnHidden() { }
}
