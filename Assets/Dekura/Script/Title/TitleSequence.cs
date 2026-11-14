using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace Title
{
    public enum State { LogoBack, Logo, PressKey, WaitInput ,IdleMovie}

    public class TitleSequence : MonoBehaviour
    {
        [Header("表示物")]
        [SerializeField] private Transform TitleUiLoot;
        [SerializeField] private CanvasGroup logoBack;
        [SerializeField] private CanvasGroup logo;
        [SerializeField] private CanvasGroup pressKeyText;
        [SerializeField] private MenuDeploy buttons;

        [Header("演出")]
        [SerializeField] private float logoBackDuration = 1.5f;
        [SerializeField] private float logoDuration = 1.5f;
        [SerializeField] private float pressKeyDuration = 1f;
        [SerializeField] private float blinkInterval = 0.8f;
        [SerializeField] private float blinkMinAlpha = 0.3f;

        [Header("放置動画")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private GameObject videoScreen;
        [SerializeField] private float idleWaitTime = 8f;

        //-----------------------------------------------------------------------------//

        private State state;
        private Coroutine current;
        private Tween blinkTween;
        private bool decided;

        private float idleTimeCount = 0f;

        public bool InputEnabled = true;

        private void Start()
        {
            logoBack.alpha = 0f;
            logo.alpha = 0f;
            pressKeyText.alpha = 0f;

            videoPlayer.loopPointReached += OnMovieEnd;
            videoPlayer.Prepare();   // 事前に準備しておくと再生開始が速い
            videoScreen.SetActive(false);

            enabled = false;
        }

        private void Update()
        {
            if (decided)
            {
                if (InputEnabled && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    decided = false;
                    buttons.enabled = false;

                    blinkTween.Play();
                    ApplyWaitInputView();
                    return;
                }
            }

            if (InputEnabled && Mouse.current.leftButton.wasPressedThisFrame)
            {
                idleTimeCount = 0f;

                if (ScreenFader.IsFading) return;

                if (state == State.IdleMovie)
                {
                    Restart();
                }
                else if (state != State.WaitInput)
                {
                    ChangeState(State.WaitInput);
                }
                else if (!decided)
                {
                    decided = true;
                    buttons.enabled = true;

                    blinkTween.Pause();
                    hideInputView();
                }
                return;
            }

            if (state == State.WaitInput && !decided)
            {
                idleTimeCount += Time.deltaTime;
                if (idleTimeCount >= idleWaitTime && !ScreenFader.IsFading) ScreenFader.FadeOutIn(0.5f, 0.5f, () => ChangeState(State.IdleMovie));
            }
        }

        // タイトル演出開始地点
        public void Restart()
        {
            StopMovie();
            KillTweens();
            hideInputView();
            idleTimeCount = 0f;
            decided = false;

            ChangeState(State.LogoBack);
        }

        private void ChangeState(State next)
        {
            state = next;

            if (current != null) StopCoroutine(current);   // 実行中の演出コルーチンを止める
            current = StartCoroutine(RunState(next));
        }

        private IEnumerator RunState(State s)
        {
            switch (s)
            {
                case State.LogoBack:
                    yield return Fade(logoBack, logoBackDuration).WaitForCompletion();
                    ChangeState(State.Logo);
                    break;

                case State.Logo:
                    yield return Fade(logo, logoDuration).WaitForCompletion();
                    ChangeState(State.PressKey);
                    break;

                case State.PressKey:
                    yield return Fade(pressKeyText, pressKeyDuration).WaitForCompletion();
                    ChangeState(State.WaitInput);
                    break;

                case State.WaitInput:
                    KillTweens();
                    ApplyWaitInputView();
                    StartBlink();
                    break;

                case State.IdleMovie:
                    KillTweens();
                    yield return PlayMovie();
                    break;
            }
        }

        private Tween Fade(CanvasGroup target, float duration)
        {
            return target.DOFade(1f, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }

        // 入力待機(最終状態)の見た目
        private void ApplyWaitInputView()
        {
            logoBack.alpha = 1f;
            logo.alpha = 1f;
            pressKeyText.alpha = 1f;
        }
        private void hideInputView()
        {
            logoBack.alpha = 0f;
            logo.alpha = 0f;
            pressKeyText.alpha = 0f;
        }


        private void StartBlink()
        {
            blinkTween = pressKeyText.DOFade(blinkMinAlpha, blinkInterval)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void KillTweens()
        {
            logoBack.DOKill();
            logo.DOKill();
            pressKeyText.DOKill();
            blinkTween = null;
        }

        private IEnumerator PlayMovie()
        {
            if (!videoPlayer.isPrepared)
            {
                videoPlayer.Prepare();
                while (!videoPlayer.isPrepared) yield return null;
            }

            videoPlayer.Play();
            videoScreen.SetActive(true);
        }

        private void StopMovie()
        {
            videoPlayer.Stop();
            videoScreen.SetActive(false);
        }

        private void OnMovieEnd(VideoPlayer source)
        {
            if (state == State.IdleMovie) ScreenFader.FadeOutIn(0.5f, 0.5f, Restart);   // 最後まで流れたらタイトルへ戻す
        }

        public void StartSequence()
        {
            enabled = true;
            Restart();
        }
    }
}