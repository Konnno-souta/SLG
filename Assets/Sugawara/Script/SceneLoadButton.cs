using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ボタンを押したら指定したシーンへ移動する（どのシーンでも使える）
// 使い方：ボタンの OnClick に Load() を登録する
public class SceneLoadButton : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("移動するシーン（Project からシーンファイルをドラッグ）")]
    [SerializeField] UnityEditor.SceneAsset scene;
#endif
    [SerializeField, HideInInspector] string sceneName;

    [Header("フェード（任意）")]
    [Tooltip("画面を暗くしてから移動する。空ならすぐ移動")]
    [SerializeField] MapScreenFader fader;
    [SerializeField] float fadeDuration = 0.5f;

    bool loading;

    public void Load()
    {
        if (loading) return; // 2回押し防止

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[SceneLoadButton] {name} の Scene が設定されていません", this);
            return;
        }
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        loading = true;
        if (fader != null) yield return StartCoroutine(fader.FadeOut(fadeDuration));
        SceneManager.LoadScene(sceneName);
    }

#if UNITY_EDITOR
    // ドラッグしたシーンの名前を保存しておく（ビルド後も使えるように）
    void OnValidate()
    {
        sceneName = scene != null ? scene.name : "";
    }
#endif
}
