using GameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Examples
{
    /// <summary>
    /// 文字效果完整示例页。
    /// 外观来自框架组件库的 Popup - Size Fitter 副本；本类只负责演示内容和页面生命周期。
    /// </summary>
    [UIPage("UI/UITextEffectsDemoPage", UIOpenMode.Single, UILayer.Popup, UILifetime.Scene)]
    public sealed class UITextEffectsDemoPage : UIPage, IController
    {
        private TMP_Text mText;
        private UITextAnimator mAnimator;
        private Button mNextButton;
        private Button mReplayButton;
        private Button mCloseButton;
        private int mGroupIndex;

        private static readonly string[] sDemoGroups =
        {
            "入场效果\n" +
            "Fade In：<fx=fade_in once>任务已接取</fx>\n" +
            "Scale In：<fx=scale_in once>等级提升</fx>\n" +
            "Typewriter：<fx=typewriter once>警告，前方危险！请谨慎前进。</fx>",

            "AE 风格入场\n" +
            "Pop In：<fx=pop_in once>获得新装备</fx>\n" +
            "Spin Drop：<fx=spin_drop once>任务完成</fx>\n" +
            "Flip In：<fx=flip_in once>区域已解锁</fx>",

            "AE 风格 · 粒子\n" +
            "Fly In：<fx=fly_in once>系统已启动</fx>\n" +
            "Shatter Snap：<fx=shatter_snap once>数据重组完成</fx>",

            "持续位移\n" +
            "Shake：<fx=idle_shake loop>危险区域</fx>\n" +
            "Float：<fx=idle_float loop>漂浮的地图标记</fx>\n" +
            "Rotate：<fx=idle_rotate loop>旋转提示</fx>\n" +
            "Wave：<fx=idle_wave loop>波浪文字</fx>",

            "颜色与强调\n" +
            "Pulse：<fx=color_pulse loop>颜色脉冲</fx>\n" +
            "Rare：<color=#FFD45A><fx=highlight_rare loop>五星奖励</fx></color>\n" +
            "Warning：<color=#FF6B6B><fx=highlight_warning loop>生命值过低</fx></color>\n" +
            "Gradient：<fx=gradient_flow loop>自定义渐变标题</fx>",

            "节奏与组合\n" +
            "Heartbeat：<fx=heartbeat loop>重要通知</fx>\n" +
            "Bounce：<fx=bounce loop>弹跳提示</fx>\n" +
            "Jitter：<fx=jitter loop>网络连接不稳定</fx>\n" +
            "Legendary：<fx=reward_legendary loop>传奇奖励</fx>",

            "扫光与冲击\n" +
            "Shine Sweep：<fx=shine_sweep loop>秘境开启</fx>\n" +
            "Glitch Burst：<fx=glitch_burst loop>信号异常</fx>\n" +
            "Glitch Slice：<fx=glitch_slice loop>画面撕裂</fx>",

            "蓄力与扫描\n" +
            "Charge Strike：<fx=charge_strike once>传说装备觉醒</fx>\n" +
            "Pulse Scanner：<fx=pulse_scanner loop>正在解密数据流</fx>",

            "嵌套效果\n" +
            "<fx=typewriter once>Heart and soul <fx=rainbow loop>狗饺</fx>唱歌，" +
            "<color=#FF6B6B><fx=highlight_warning loop>加长</fx></color>版。</fx>"
        };

        public override void OnOpen()
        {
            mText = FindDemoText();
            mAnimator = mText == null ? null : mText.GetComponent<UITextAnimator>();
            BindButtons();

            if (mText == null || mAnimator == null)
            {
                Debug.LogError("UITextEffectsDemoPage 缺少演示用 TMP_Text 或 UITextAnimator。", this);
            }
        }

        /// <summary>
        /// 绑定演示页按钮。三个按钮时依次是"下一组 / 重播 / 关闭"，
        /// 只有两个按钮时保持旧布局"下一组 / 关闭"。
        /// </summary>
        private void BindButtons()
        {
            var buttons = transform.Find("Buttons")?.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
            {
                return;
            }

            mNextButton = buttons[0];
            if (buttons.Length >= 3)
            {
                mReplayButton = buttons[1];
                mCloseButton = buttons[2];
            }
            else if (buttons.Length >= 2)
            {
                mCloseButton = buttons[1];
            }

            if (mNextButton != null)
            {
                mNextButton.GetComponentInChildren<TMP_Text>(true).text = "下一组";
                mNextButton.onClick.AddListener(NextGroup);
            }

            if (mReplayButton != null)
            {
                mReplayButton.GetComponentInChildren<TMP_Text>(true).text = "重播";
                mReplayButton.onClick.AddListener(Replay);
            }

            if (mCloseButton != null)
            {
                mCloseButton.GetComponentInChildren<TMP_Text>(true).text = "关闭";
                mCloseButton.onClick.AddListener(CloseSelf);
            }
        }

        public override void OnShow()
        {
            mGroupIndex = 0;
            PlayDemo();
        }

        private void PlayDemo()
        {
            if (mAnimator == null)
            {
                return;
            }

            mAnimator.Play(sDemoGroups[mGroupIndex]);
        }

        private void NextGroup()
        {
            mGroupIndex = (mGroupIndex + 1) % sDemoGroups.Length;
            PlayDemo();
        }

        /// <summary>重播当前分组：先恢复原始网格，避免从动画中间状态叠加播放。</summary>
        private void Replay()
        {
            if (mAnimator == null)
            {
                return;
            }

            mAnimator.Stop(true);
            PlayDemo();
        }

        public override void OnClose()
        {
            if (mNextButton != null)
            {
                mNextButton.onClick.RemoveListener(NextGroup);
            }

            if (mReplayButton != null)
            {
                mReplayButton.onClick.RemoveListener(Replay);
            }

            if (mCloseButton != null)
            {
                mCloseButton.onClick.RemoveListener(CloseSelf);
            }

            mAnimator?.Stop(true);
        }

        private TMP_Text FindDemoText()
        {
            var text = transform.Find("Content/Text (TMP)");
            return text == null ? GetComponentInChildren<TMP_Text>(true) : text.GetComponent<TMP_Text>();
        }

        private void CloseSelf()
        {
            // 关闭直接生效：不做退场动画，避免出现可感知的延迟。
            this.GetUtility<IUGUIService>().Close(this);
        }

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
