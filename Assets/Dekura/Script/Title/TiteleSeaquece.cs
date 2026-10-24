using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Title
{
    public class TiteleSeaquece : MonoBehaviour
    {
        public enum State { Title, PressKey, Menu ,IdleMovie}

        [Header("タイトルロゴ")]
        public Image titleImage_base;
        public float popDiray_Base = 1f;
        public Image titleImage_Title;
        public float popDiray_Title = 1f;

        [Header("PressKey表示")]
        public TextMeshProUGUI text_PressKey;
        public float popDiray_PressKey = 1f;
        public float flashDiray_PressKey = 0.5f;

        [Header("メニュー")]
        public GameObject menuRoot;


        [Header("待機時間")]
        public float idleTime = 8f;
        public VideoPlayer videoPlayer;
        public GameObject videoScreen;

        //----------------------------------------------------------------------------//
        private State nowState;
        private float timer = 0f;

        private Coroutine current;
        private bool isPlayingMovie;
        private bool isPerforming;     // 演出中か
        private bool skipRequested;    // 演出スキップ要求
        private bool keyPressed;       // 演出外での入力(PressKeyの決定用)

        private void Awake()
        {
            videoScreen.SetActive(false);
            titleImage_base.DOFade(0f,0f);
            titleImage_Title.DOFade(0f, 0f);
            text_PressKey.DOFade(0f, 0f);
        }

        private void Start() => ChangeState(State.Title);

        private void Update()
        {
            if (timer > idleTime && isPlayingMovie)
            {
                ChangeState(State.IdleMovie);
                isPlayingMovie = true;
            }
            else if(timer > idleTime)
            {
                timer += Time.deltaTime;
            }

            if (!Input.anyKeyDown) return;

            timer = 0f;

            if (isPerforming) skipRequested = true;
            else keyPressed = true;
        }

        private void ChangeState(State next)
        {
            nowState = next;
            skipRequested = false;
            keyPressed = false;

            if (current != null) StopCoroutine(current);
            current = StartCoroutine(RunState(next));
        }

        private IEnumerator RunState(State s)
        {
            switch (s)
            {
                case State.Title:
                    yield return PerformTween(DOTweenSequence());
                    ChangeState(State.PressKey);
                    break;

                case State.PressKey:
                    yield return PerformTween(DOTweenSequence());
                    yield return new WaitUntil(() => keyPressed);
                    ChangeState(State.Menu);
                    break;

                case State.Menu:
                    break;

                case State.IdleMovie:
                    yield return new WaitUntil(() => keyPressed);
                    break;
            }
        }

        private IEnumerator PerformTween(Tween tween)
        {
            isPerforming = true;
            skipRequested = false;

            // 再生中 かつ スキップ要求なし の間待つ
            while (tween.IsActive() && tween.IsPlaying() && !skipRequested)
                yield return null;

            if (tween.IsActive()) tween.Complete();

            isPerforming = false;
            skipRequested = false;
        }

        private Sequence DOTweenSequence() => nowState switch
        {
            State.Title => DOTween.Sequence()
                                  .Append(titleImage_base.DOFade(1f, popDiray_Base))
                                  .Join(titleImage_Title.DOFade(1f, popDiray_Title)),

            State.PressKey => DOTween.Sequence()
                                     .Append(titleImage_base.DOFade(1f, popDiray_PressKey)),
            _ => default,
        };

        private void MoviePlayer(bool mode)
        {
            videoScreen.SetActive(true);
            videoPlayer.Play();
        }
    }
}

