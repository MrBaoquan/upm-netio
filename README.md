## NetIO 设备管理平台

### 快速使用

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UNIHper;
using UniRx;

public class SceneEntryScript : SceneScriptBase
{
    private void Awake()
    {
        NetIOBuilder.AutoBuild();
    }

    // Called once after scene is loaded
    private void Start()
    {
        Managements.UI.Show<NetIOControlUI>();
    }
}

```
