using UnityEngine;

namespace CutIn
{
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
        [Tooltip("帯の中での位置（帯の中心からの距離 px）。顔が帯の真ん中に来るように合わせる")]
        public Vector2 characterOffset = new Vector2(0f, -150f);
        [Tooltip("左右反転")]
        public bool flipX = false;
        [Tooltip("画像の傾き（度）")]
        public float characterRotation = 0f;
        [Tooltip("画像の色味（白でそのまま）")]
        public Color characterColor = Color.white;
        [Tooltip("止まっている間にゆっくり動く速さ (px/秒)。右へ流れる感じを出す")]
        public Vector2 drift = new Vector2(40f, 0f);

        [Header("帯")]
        [Tooltip("帯の高さ")]
        public float bandHeight = 380f;
        [Tooltip("帯の傾き（度）。0で水平、マイナスで右下がり")]
        public float bandAngle = 0f;
        [Tooltip("帯の背景画像（空なら下の2色のグラデーション）")]
        public Sprite bandBackground;
        public Color bandColorLeft = new Color(0.05f, 0.12f, 0.35f, 1f);
        public Color bandColorRight = new Color(0.45f, 0.75f, 1f, 1f);
        [Tooltip("帯の上下のふちの線（透明にすると無し）")]
        public Color bandEdgeColor = new Color(1f, 1f, 1f, 0.9f);
        public float bandEdgeThickness = 6f;

        [Header("流れる線")]
        public bool speedLines = true;
        public Color speedLineColor = new Color(1f, 1f, 1f, 0.55f);

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
