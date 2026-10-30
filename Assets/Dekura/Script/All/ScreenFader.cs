using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ScreenFader
{
    private static CanvasGroup group;
    private static Tween tween;

    // フェードのTweenが動いている間は true
    public static bool IsFading => tween != null && tween.IsActive();

    public static Tween FadeOut(float duration, Action onComplete = null)
        => Fade(1f, duration, onComplete);

    public static Tween FadeIn(float duration, Action onComplete = null)
        => Fade(0f, duration, onComplete);

    public static void FadeOutIn(float outDuration, float inDuration, Action onBlack = null, Action onComplete = null)
    {
        FadeOut(outDuration, () =>
        {
            onBlack?.Invoke();
            FadeIn(inDuration, onComplete);
        });
    }

    // 暗転 → シーン読み込み → 明ける
    public static void LoadScene(string sceneName, float outDuration = 0.5f, float inDuration = 0.5f)
    {
        FadeOut(outDuration, () =>
        {
            UnityAction<Scene, LoadSceneMode> onLoaded = null;
            onLoaded = (scene, mode) =>
            {
                SceneManager.sceneLoaded -= onLoaded;
                FadeIn(inDuration);
            };
            SceneManager.sceneLoaded += onLoaded;
            SceneManager.LoadScene(sceneName);
        });
    }

    private static Tween Fade(float targetAlpha, float duration, Action onComplete)
    {
        Create();
        group.DOKill();
        group.blocksRaycasts = true;   // フェード中はUI操作を受け付けない

        bool isClear = targetAlpha <= 0f;
        tween = group.DOFade(targetAlpha, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)           // Time.timeScale が 0 でも動く
            .OnComplete(() =>
            {
                if (isClear) group.blocksRaycasts = false;
                onComplete?.Invoke();
            });
        return tween;
    }

    // 黒い全画面Canvasをコードで生成(シーンをまたいで残る)
    private static void Create()
    {
        if (group != null) return;

        GameObject go = new GameObject("ScreenFader");
        UnityEngine.Object.DontDestroyOnLoad(go);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // 最前面

        Image image = go.AddComponent<Image>();
        image.color = Color.black;

        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }
}