using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CutIn
{
    public enum DemoMode
    {
        PressButton,  // ボタンを押すたびに再生
        AutoLoop,     // 全員を順番に自動で流し続ける
        AutoOnce      // 全員を順番に1回だけ流す
    }

    /// <summary>
    /// カットインのデモ用。登録したカットインを、キー・ボタンで再生したり、順番に自動で流したりする。
    ///   再生             : Space / Enter / Xbox の A
    ///   前・次に切り替え : ← → / Xbox の LB・RB（十字キー左右でも可）
    ///   自動再生の切替   : P / Xbox の Menu
    /// </summary>
    public class CutInDemo : MonoBehaviour
    {
        [Header("カットイン")]
        [Tooltip("空なら同じ GameObject かシーン内から探す（無ければ自動で追加）")]
        public CutInPlayer cutInPlayer;
        [Tooltip("流すカットイン（上から順番）")]
        public List<CutInData> cutIns = new List<CutInData>();

        [Header("流し方")]
        public DemoMode mode = DemoMode.PressButton;
        [Tooltip("自動再生の時、次のカットインまでの間（秒）")]
        public float interval = 1f;
        [Tooltip("自動再生を始めるまでの待ち時間（秒）")]
        public float startDelay = 0.5f;
        [Tooltip("順番ではなくランダムに選ぶ")]
        public bool randomOrder = false;

        [Header("操作の説明（画面左下）")]
        public bool showHelp = true;
        public float helpSize = 28f;
        public Color helpColor = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>今選ばれているカットインの番号</summary>
        public int CurrentIndex { get; private set; }

        Coroutine autoRoutine;
        TextMeshProUGUI helpText;

        void Start()
        {
            if (!cutInPlayer && !TryGetComponent(out cutInPlayer)) cutInPlayer = FindAnyObjectByType<CutInPlayer>();
            if (!cutInPlayer) cutInPlayer = gameObject.AddComponent<CutInPlayer>();
            cutIns.RemoveAll(c => c == null);

            if (cutIns.Count == 0)
                Debug.LogWarning("[CutInDemo] Cut Ins にカットインが登録されていません", this);

            if (showHelp) BuildHelp();
            if (mode != DemoMode.PressButton) StartAuto();
            UpdateHelp();
        }

        void Update()
        {
            if (cutIns.Count == 0) return;

            ReadInput(out bool play, out bool prev, out bool next, out bool toggleAuto);

            if (toggleAuto)
            {
                if (autoRoutine != null) StopAuto();
                else StartAuto();
            }

            if (prev) Select(CurrentIndex - 1);
            if (next) Select(CurrentIndex + 1);
            if (play) PlayCurrent();

            if (prev || next || toggleAuto) UpdateHelp();
        }

        // =====================================================================
        // 公開API（ボタンやイベントからも呼べる）
        // =====================================================================

        /// <summary>今選ばれているカットインを再生</summary>
        [ContextMenu("今のカットインを再生")]
        public void PlayCurrent()
        {
            if (!Application.isPlaying || cutIns.Count == 0) return;
            cutInPlayer.Play(cutIns[CurrentIndex]);
            UpdateHelp();
        }

        /// <summary>次のカットインを選んで再生</summary>
        [ContextMenu("次のカットインを再生")]
        public void PlayNext()
        {
            if (!Application.isPlaying || cutIns.Count == 0) return;
            Select(randomOrder ? RandomOther() : CurrentIndex + 1);
            PlayCurrent();
        }

        /// <summary>番号を指定して再生（0から）</summary>
        public void PlayAt(int index)
        {
            Select(index);
            PlayCurrent();
        }

        public void Select(int index)
        {
            if (cutIns.Count == 0) return;
            CurrentIndex = ((index % cutIns.Count) + cutIns.Count) % cutIns.Count;
        }

        public void StartAuto()
        {
            StopAuto();
            autoRoutine = StartCoroutine(AutoRoutine());
            UpdateHelp();
        }

        public void StopAuto()
        {
            if (autoRoutine != null) StopCoroutine(autoRoutine);
            autoRoutine = null;
            UpdateHelp();
        }

        IEnumerator AutoRoutine()
        {
            yield return new WaitForSecondsRealtime(startDelay);
            int played = 0;
            while (cutIns.Count > 0)
            {
                if (played > 0) Select(randomOrder ? RandomOther() : CurrentIndex + 1);
                UpdateHelp();
                yield return cutInPlayer.PlayAndWait(cutIns[CurrentIndex]);
                played++;

                if (mode == DemoMode.AutoOnce && played >= cutIns.Count) break;
                yield return new WaitForSecondsRealtime(interval);
            }
            autoRoutine = null;
            UpdateHelp();
        }

        int RandomOther()
        {
            if (cutIns.Count <= 1) return 0;
            int i;
            do i = Random.Range(0, cutIns.Count); while (i == CurrentIndex);
            return i;
        }

        // =====================================================================
        // 操作の説明
        // =====================================================================

        void BuildHelp()
        {
            var canvasGo = new GameObject("CutInDemoHelp", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (cutInPlayer ? cutInPlayer.sortingOrder : 200) - 1;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Help", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            helpText = go.AddComponent<TextMeshProUGUI>();
            if (cutInPlayer && cutInPlayer.font) helpText.font = cutInPlayer.font;
            helpText.fontSize = helpSize;
            helpText.color = helpColor;
            helpText.alignment = TextAlignmentOptions.BottomLeft;
            helpText.raycastTarget = false;
            var rt = helpText.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(32f, 24f);
            rt.sizeDelta = new Vector2(1200f, 200f);
        }

        void UpdateHelp()
        {
            if (!helpText) return;
            string current = cutIns.Count > 0 && cutIns[CurrentIndex] ? cutIns[CurrentIndex].name : "（未登録）";
            string auto = autoRoutine != null ? "自動再生中" : "手動";
            helpText.text =
                $"[{CurrentIndex + 1}/{cutIns.Count}] {current}  ({auto})\n" +
                "Space / A : 再生    ← → / LB RB : 切り替え    P / Menu : 自動再生";
        }

        // =====================================================================
        // 入力
        // =====================================================================

        static void ReadInput(out bool play, out bool prev, out bool next, out bool toggleAuto)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            play = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
            prev = kb != null && kb.leftArrowKey.wasPressedThisFrame;
            next = kb != null && kb.rightArrowKey.wasPressedThisFrame;
            toggleAuto = kb != null && kb.pKey.wasPressedThisFrame;

            foreach (var gp in Gamepad.all)
            {
                play |= gp.buttonSouth.wasPressedThisFrame;
                prev |= gp.leftShoulder.wasPressedThisFrame || gp.dpad.left.wasPressedThisFrame;
                next |= gp.rightShoulder.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame;
                toggleAuto |= gp.startButton.wasPressedThisFrame;
            }
#else
            play = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0);
            prev = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton4);
            next = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton5);
            toggleAuto = Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }
    }
}
