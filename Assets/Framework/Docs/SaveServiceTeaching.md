# SaveService 本地存档教学

## 一、这一部分要解决什么问题

`SaveService` 负责把需要跨游戏重启保留的数据写入本地文件，并在下次启动时读取。

第一阶段先实现用户设置：

    音量
    画质等级
    全屏开关
    语言

它以后也可以保存玩家进度、背包、货币和关卡解锁状态。

本教程只新增教学文档，不要求现在修改项目源码。请先手抄、理解，再决定是否加入框架。

## 二、先确定职责边界

`SaveService` 负责：

    把数据转换为 JSON
    写入 Application.persistentDataPath
    从文件读取 JSON
    文件不存在时返回失败
    文件损坏时返回失败
    删除存档文件

`SaveService` 不负责：

    决定什么时候保存
    决定设置页面长什么样
    修改音量或画质本身
    连接云端服务器
    加密存档

页面或游戏系统负责修改数据，SaveService 只负责保存和读取。

## 三、创建存档接口

新建文件：

    Assets/Framework/Runtime/Save/ISaveService.cs

手抄以下代码：

    using System;

    namespace GameFramework
    {
        public interface ISaveService : IUtility
        {
            bool Save<T>(string key, T data);
            bool TryLoad<T>(string key, out T data);
            bool Delete(string key);
            bool Exists(string key);
        }
    }

这里使用 `TryLoad`，因为读取文件可能失败，调用方需要根据返回值决定是否使用默认数据。

## 四、创建设置数据类

新建文件：

    Assets/Framework/Runtime/Save/UserSettings.cs

手抄以下代码：

    using System;

    namespace GameFramework
    {
        [Serializable]
        public sealed class UserSettings
        {
            public float MasterVolume = 1f;
            public float MusicVolume = 1f;
            public float EffectsVolume = 1f;
            public int QualityLevel = 2;
            public bool FullScreen = true;
            public string Language = "zh-CN";
        }
    }

`JsonUtility` 只能处理 Unity 支持序列化的字段，因此字段使用 `public`，类型保持简单。

## 五、实现 JSON 存档服务

新建文件：

    Assets/Framework/Runtime/Save/JsonSaveService.cs

手抄以下代码：

    using System;
    using System.IO;
    using UnityEngine;

    namespace GameFramework
    {
        public sealed class JsonSaveService : ISaveService
        {
            private readonly string mRootPath;

            public JsonSaveService()
            {
                mRootPath = Path.Combine(Application.persistentDataPath, "Saves");
                Directory.CreateDirectory(mRootPath);
            }

            public bool Save<T>(string key, T data)
            {
                if (string.IsNullOrWhiteSpace(key) || data == null)
                {
                    return false;
                }

                try
                {
                    var json = JsonUtility.ToJson(data, true);
                    File.WriteAllText(GetPath(key), json);
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return false;
                }
            }

            public bool TryLoad<T>(string key, out T data)
            {
                data = default(T);
                if (string.IsNullOrWhiteSpace(key))
                {
                    return false;
                }

                var path = GetPath(key);
                if (!File.Exists(path))
                {
                    return false;
                }

                try
                {
                    var json = File.ReadAllText(path);
                    data = JsonUtility.FromJson<T>(json);
                    return data != null;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    data = default(T);
                    return false;
                }
            }

            public bool Delete(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return false;
                }

                var path = GetPath(key);
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                return true;
            }

            public bool Exists(string key)
            {
                return !string.IsNullOrWhiteSpace(key) && File.Exists(GetPath(key));
            }

            private string GetPath(string key)
            {
                if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException("Save key contains invalid characters.", nameof(key));
                }

                return Path.Combine(mRootPath, key + ".json");
            }
        }
    }

## 六、在 Architecture 中注册

文件：

    Assets/Example/Scripts/ExampleArchitecture.cs

在 `Init()` 中注册：

    RegisterUtility<ISaveService>(new JsonSaveService());

建议放在资源加载器附近：

    RegisterUtility<IAssetLoader>(new ResourcesAssetLoader());
    RegisterUtility<ISaveService>(new JsonSaveService());
    RegisterUtility<ITimerService>(new TimerService());

## 七、读取设置

可以在设置系统启动时使用：

    var save = this.GetUtility<ISaveService>();
    UserSettings settings;

    if (!save.TryLoad("user_settings", out settings))
    {
        settings = new UserSettings();
        save.Save("user_settings", settings);
    }

读取流程是：

    有文件且 JSON 正常
    -> 使用文件中的设置

    没有文件
    -> 创建默认设置并保存

    文件损坏
    -> TryLoad 返回 false，使用默认设置

## 八、保存设置

设置页面修改值后：

    settings.MasterVolume = 0.75f;
    settings.QualityLevel = 3;
    settings.FullScreen = false;

    var success = this.GetUtility<ISaveService>()
        .Save("user_settings", settings);

正式项目中可以选择“点击应用时保存”，也可以选择“退出设置页面时保存”。保存时机属于业务决定，不由 SaveService 强行规定。

## 九、文件位置

Unity 会把文件保存到：

    Application.persistentDataPath/Saves/user_settings.json

不同平台的实际路径不同，所以代码不能把 Windows 路径写死。调试时可以输出：

    Debug.Log(Application.persistentDataPath);

## 十、这一版的限制

第一版故意不加入：

    多存档槽位
    存档版本迁移
    加密
    压缩
    云同步
    写入临时文件后替换正式文件
    存档校验和

这些功能不是 `SaveService` 的基础读写条件。先把本地设置保存和读取流程验证清楚，再根据实际需求增加。

## 十一、手抄后的验证顺序

    1. 注册 JsonSaveService。
    2. 创建 UserSettings 并保存。
    3. 关闭并重新运行游戏。
    4. 读取 user_settings.json。
    5. 确认修改过的音量和画质仍然存在。
    6. 删除文件后确认读取失败并回退默认值。
    7. 手动写入错误 JSON，确认 TryLoad 返回 false。

完成这一步后，下一阶段再考虑把 `UserSettings` 与设置页面、音频服务和画质服务连接起来。
