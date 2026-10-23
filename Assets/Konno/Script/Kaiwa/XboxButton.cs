using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Conversation
{
    /// <summary>Xbox コントローラーのボタン</summary>
    public enum XboxButton
    {
        A, B, X, Y,
        LB, RB,          // 左右のバンパー
        LT, RT,          // 左右のトリガー
        View,            // ビューボタン（左の小さいボタン）
        Menu,            // メニューボタン（右の小さいボタン）
        LeftStick,       // 左スティック押し込み
        RightStick       // 右スティック押し込み
    }

    /// <summary>Xbox のボタン名から、実際のコントローラーのボタンを取り出す</summary>
    public static class XboxPad
    {
#if ENABLE_INPUT_SYSTEM
        /// <summary>Xbox のボタン名を、コントローラーのボタンに変換</summary>
        public static ButtonControl GetButton(Gamepad gp, XboxButton button)
        {
            if (gp == null) return null;
            switch (button)
            {
                case XboxButton.A: return gp.buttonSouth;
                case XboxButton.B: return gp.buttonEast;
                case XboxButton.X: return gp.buttonWest;
                case XboxButton.Y: return gp.buttonNorth;
                case XboxButton.LB: return gp.leftShoulder;
                case XboxButton.RB: return gp.rightShoulder;
                case XboxButton.LT: return gp.leftTrigger;
                case XboxButton.RT: return gp.rightTrigger;
                case XboxButton.View: return gp.selectButton;
                case XboxButton.Menu: return gp.startButton;
                case XboxButton.LeftStick: return gp.leftStickButton;
                case XboxButton.RightStick: return gp.rightStickButton;
            }
            return null;
        }
#else
        /// <summary>
        /// 旧 Input Manager での Xbox コントローラーのボタン番号（Windows）。
        /// LT / RT はボタンではなく軸なので、旧 Input Manager では使えない
        /// </summary>
        public static KeyCode LegacyKeyCode(XboxButton button)
        {
            switch (button)
            {
                case XboxButton.A: return KeyCode.JoystickButton0;
                case XboxButton.B: return KeyCode.JoystickButton1;
                case XboxButton.X: return KeyCode.JoystickButton2;
                case XboxButton.Y: return KeyCode.JoystickButton3;
                case XboxButton.LB: return KeyCode.JoystickButton4;
                case XboxButton.RB: return KeyCode.JoystickButton5;
                case XboxButton.View: return KeyCode.JoystickButton6;
                case XboxButton.Menu: return KeyCode.JoystickButton7;
                case XboxButton.LeftStick: return KeyCode.JoystickButton8;
                case XboxButton.RightStick: return KeyCode.JoystickButton9;
            }
            return KeyCode.None;
        }
#endif
    }
}
