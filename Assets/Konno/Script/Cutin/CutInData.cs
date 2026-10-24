using System;
using System.Collections.Generic;
using UnityEngine;

namespace CutIn
{
    /// <summary>キャラが止まった後の動き</summary>
    public enum CharacterMotion
    {
        PullBackAndDash,  // 少し左に下がってから、右へ走り抜けて画面の外へ
        Stay              // そのまま（帯と一緒に消える）
    }

    /// <summary>帯の後ろに重ねる帯（少しずらして、少し遅れて入ってくる）</summary>
    [Serializable]
    public class BandLayer
    {
        public Color color = Color.black;
        [Tooltip("メインの帯より何 px 太くするか")]
        public float extraHeight = 60f;
        [Tooltip("メインの帯からのずれ（x: 帯に沿った方向、y: 帯に垂直な方向）")]
        public Vector2 offset = new Vector2(0f, -20f);
        [Tooltip("メインの帯より何秒遅れて入ってくるか")]
        public float delay = 0.04f;
    }

    /// <summary>
    /// キャラクター1人分のカットイン設定。
    /// Project ウィンドウで右クリック → Create > CutIn > Cut-In Data で作る。
    /// </summary>
    [CreateAssetMenu(menuName = "CutIn/Cut-In Data", fileName = "CutIn_")]
    public class CutInData : ScriptableObject
    {
        [Header("キャラクター画像")]
        public Sprite character;
        [Tooltip("画像の大きさ（元の画像サイズに対する倍率）")]
        public float characterScale = 1f;
        [Tooltip("帯の中での位置（帯の中心からの距離 px）。マイナスで左・下")]
        public Vector2 characterOffset = new Vector2(0f, -150f);
        [Tooltip("左右反転")]
        public bool flipX = false;
        [Tooltip("画像の傾き（度）")]
        public float characterRotation = 0f;
        [Tooltip("帯を傾けても、キャラ画像はまっすぐのままにする")]
        public bool characterUpright = false;
        [Tooltip("画像の色味（白でそのまま）")]
        public Color characterColor = Color.white;
        [Tooltip("止まっている間にゆっくり動く速さ (px/秒)。0 でぴたっと止まる")]
        public Vector2 drift = Vector2.zero;

        [Header("キャラの動き")]
        [Tooltip("PullBackAndDash: FE 風（行き過ぎ → 左へ戻る → ゆっくり右へ → 一気に抜ける） / Stay: 止まったまま")]
        public CharacterMotion motion = CharacterMotion.PullBackAndDash;
        [Tooltip("左から入ってくる時間（秒）")]
        public float enterTime = 0.12f;
        [Tooltip("入ってきた時に真ん中を行き過ぎる距離 (px)。この分だけ左へ戻る")]
        public float pullBackDistance = 70f;
        [Tooltip("行き過ぎたところで止まる時間（秒）。0 で止まらずにすぐ戻る")]
        public float holdBeforePullBack = 0f;
        [Tooltip("左へ戻る時間（秒）。だんだんゆっくりになる")]
        public float pullBackTime = 0.33f;
        [Tooltip("左へ戻る時に、時計回りに傾ける角度（度）。0 で傾けない")]
        public float pullBackRotation = 8f;
        [Tooltip("ゆっくり右へ動き出す距離 (px)")]
        public float creepDistance = 60f;
        [Tooltip("ゆっくり右へ動き出す時間（秒）。だんだん速くなる")]
        public float creepTime = 0.45f;
        [Tooltip("一気に右へ走り抜ける時間（秒）。短いほど速い")]
        public float dashTime = 0.3f;
        [Tooltip("右へ走り抜ける距離 (px)。画面の外まで出る長さにする")]
        public float dashDistance = 2400f;
        [Tooltip("走り抜ける時に、傾きをまっすぐに戻す")]
        public bool straightenOnDash = true;
        [Tooltip("走り抜けている途中から帯を閉じ始める（抜けきるのと同時に閉じ終わる）")]
        public bool closeDuringDash = true;

        [Header("帯")]
        [Tooltip("帯の形。Slash: ふちがギザギザの帯（ペルソナ風） / Rectangle: まっすぐな帯 / Wedge: 三角形")]
        public BandShape bandShape = BandShape.Slash;
        [Tooltip("Slash のふちのギザギザの大きさ")]
        public float slashJag = 18f;
        [Tooltip("Slash のギザギザの細かさ（100px あたりの折れ目の数）")]
        [Range(0.2f, 4f)] public float slashJagDensity = 1.2f;
        [Tooltip("Wedge の長さ（画面の横幅に対する割合）。左端から先端まで")]
        [Range(0.2f, 1.2f)] public float wedgeLength = 0.55f;
        [Tooltip("Wedge の左端の高さ")]
        public float wedgeHeight = 1000f;
        [Tooltip("帯の高さ（広がる形の時は右端の高さ）")]
        public float bandHeight = 460f;
        [Tooltip("右に向かって広がる形にする。左端の太さ（右端に対する割合）。1 でまっすぐ、小さいほど左が細い")]
        [Range(0.05f, 1f)] public float bandFlare = 0.35f;
        [Tooltip("帯の傾き（度）。プラスで右上がり、マイナスで右下がり、0で水平")]
        [Range(-45f, 45f)] public float bandAngle = 12f;
        [Tooltip("帯の位置（画面の中心からの距離 px）")]
        public Vector2 bandOffset = Vector2.zero;
        [Tooltip("帯の背景画像（空なら下の2色のグラデーション）")]
        public Sprite bandBackground;
        public Color bandColorLeft = new Color(0.06f, 0.06f, 0.08f, 1f);
        public Color bandColorRight = new Color(0.62f, 0.04f, 0.1f, 1f);
        [Tooltip("帯のふちの線（透明にすると無し）")]
        public Color bandEdgeColor = new Color(1f, 1f, 1f, 0.9f);
        public float bandEdgeThickness = 6f;

        [Header("後ろに重ねる帯（ずらして重ねる・空なら無し）")]
        public List<BandLayer> backLayers = new List<BandLayer>
        {
            new BandLayer { color = new Color(0.85f, 0.05f, 0.1f, 1f), extraHeight = 50f, offset = new Vector2(-30f, 18f), delay = 0.03f },
            new BandLayer { color = Color.black, extraHeight = 110f, offset = new Vector2(40f, -28f), delay = 0.06f },
        };

        [Header("網点（マンガ風の点々模様）")]
        public bool halftone = true;
        public Color halftoneColor = new Color(1f, 1f, 1f, 0.12f);
        [Tooltip("点の間隔 (px)")]
        public float halftoneSpacing = 14f;

        [Header("流れる線")]
        public bool speedLines = true;
        public Color speedLineColor = new Color(1f, 1f, 1f, 0.55f);

        [Header("雷（帯の中を横に1本走る）")]
        public bool lightning = true;
        [Tooltip("雷の見た目。Electric: 光る線 / Bolt: 塗りつぶしのギザギザの稲妻")]
        public LightningStyle lightningStyle = LightningStyle.Electric;
        [Tooltip("帯の中心からの高さ")]
        public float lightningY = 0f;
        [Tooltip("キャラの後ろに出す（オフで手前）")]
        public bool lightningBehindCharacter = true;

        [Header("雷：Bolt（塗りつぶしの稲妻）")]
        [Tooltip("左端の色")]
        public Color boltColorStart = new Color(0.98f, 0.25f, 0.25f, 1f);
        [Tooltip("先端の色")]
        public Color boltColorEnd = new Color(0.6f, 0.1f, 0.15f, 1f);
        [Tooltip("太さ")]
        public float boltThickness = 120f;
        [Tooltip("先端から何割の長さで細くとがらせるか")]
        [Range(0.05f, 1f)] public float boltTaper = 0.35f;
        [Tooltip("ギザギザの上下の大きさ")]
        public float boltAmplitude = 50f;
        [Tooltip("折れ曲がりの数")]
        [Range(3, 16)] public int boltSegments = 8;
        [Tooltip("長さ（帯の長さに対する割合）")]
        [Range(0.2f, 1f)] public float boltLength = 0.9f;
        public Color boltOutlineColor = new Color(0.3f, 0.05f, 0.05f, 0.8f);
        public float boltOutlineWidth = 3f;

        [Header("雷：Electric（光る線）")]
        [Tooltip("中心の明るい芯の色")]
        public Color lightningCoreColor = Color.white;
        [Tooltip("周りのぼんやり光る部分の色")]
        public Color lightningGlowColor = new Color(0.55f, 0.8f, 1f, 0.9f);
        [Tooltip("芯の太さ")]
        public float lightningCoreWidth = 16f;
        [Tooltip("光の広がりの太さ")]
        public float lightningGlowWidth = 90f;
        [Tooltip("ギザギザの上下の大きさ")]
        public float lightningAmplitude = 45f;
        [Tooltip("折れ曲がりの数（多いほど細かい）")]
        [Range(4, 80)] public int lightningSegments = 44;
        [Tooltip("チラつく間隔（秒）。0 でチラつかない")]
        public float lightningFlicker = 0.05f;
        [Tooltip("チラつく時に形が揺れる大きさ")]
        public float lightningJitter = 8f;

        [Header("文字（空なら無し）")]
        [Tooltip("キャラ名や技名など")]
        public string label = "";
        public Color labelColor = Color.white;
        public float labelSize = 64f;
        [Tooltip("帯の中心からの位置")]
        public Vector2 labelOffset = new Vector2(480f, -120f);

        [Header("サウンド（空なら鳴らない）")]
        [Tooltip("カットインが始まった瞬間に鳴る音")]
        public AudioClip se;
        [Tooltip("キャラが入ってきた時に鳴るボイス")]
        public AudioClip voice;
        [Range(0f, 1f)] public float volume = 1f;

        [Header("時間")]
        [Tooltip("キャラが止まって見えている時間（秒）")]
        public float holdTime = 1.2f;
    }
}