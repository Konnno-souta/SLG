using UnityEngine;

/// <summary>
/// GameOverText の確認用ボタン（画面の左上に1つだけ出る）。
/// ボタンを押すと：GAME OVER が出てくる → 上へ移動 → メニュー（説明文＋選択肢）が出る、まで全部自動で流れる。
/// 空の GameObject に付けて、Target に GAME OVER の文字（GameOverText）を入れるだけで使える。
/// EventSystem や Canvas は不要。確認が終わったら外すか、Show Button をオフにする。
/// </summary>
public class GameOverTestButtons : MonoBehaviour
{
    [Tooltip("確認する GAME OVER の文字（空なら自動で探す）")]
    public GameOverText target;
    [Tooltip("再生の前に表示するパネル（ゲームオーバーのパネル）。空なら何もしない")]
    public GameObject panel;
    [Tooltip("ゲームオーバーのメニュー（空なら自動で探す）")]
    public GameOverMenu menu;
    [Tooltip("ボタンを表示する")]
    public bool showButton = true;
    [Tooltip("ボタンの大きさ（倍）")]
    public float buttonScale = 1.5f;

    void Start()
    {
        if (!target) target = FindAnyObjectByType<GameOverText>(FindObjectsInactive.Include);
        if (!target) Debug.LogWarning("[GameOverTestButtons] GameOverText が見つかりません", this);
        if (!menu) menu = FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
    }

    void OnGUI()
    {
        if (!showButton) return;

        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(buttonScale, buttonScale, 1f));

        if (GUI.Button(new Rect(10, 10, 180, 40), "GAME OVER 確認")) PlayTest();

        GUI.matrix = old;
    }

    /// <summary>出てくる → 上へ移動 → メニュー を最初から流す</summary>
    public void PlayTest()
    {
        if (!target) return;
        if (panel && !panel.activeSelf) panel.SetActive(true);
        // 文字や親が非表示のままだと動かないので、親までまとめて表示する
        for (var t = target.transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

        if (menu) menu.HideImmediately();   // 前に出ていたメニューを消してから
        target.autoHide = false;            // 消さずにそのまま残す
        target.moveUp = true;
        target.Play();
    }
}