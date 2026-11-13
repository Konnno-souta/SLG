using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MenuDeploy : MonoBehaviour
{
    [Header("配置順(0:上)")]
    [SerializeField] private List<RectTransform> menuUis;

    [Header("配置設定")]
    [SerializeField] private Vector2 startPosition = new Vector2(50f, -50f);
    [SerializeField] private Vector2 endPosition = new Vector2(50f, -300f);

    [Header("アニメーション設定")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float stagger = 0.08f;
    [SerializeField] private Ease deployEase = Ease.OutBack;
    [SerializeField] private Ease retractEase = Ease.InBack;
    [SerializeField] private bool deployOnStart = true;

    //-----------------------------------------------------------------------------//

    private Sequence sequence;

    //-----------------------------------------------------------------------------//

    private void Awake()
    {
        foreach (var ui in menuUis)
        {
            SetInteractable(ui, false);

            ui.anchorMax = new Vector2(0f, 1f);
            ui.anchorMin = new Vector2(0f, 1f);
            ui.anchoredPosition = startPosition;
        }

        Deploy();
    }

    private void Start()
    {
        if (deployOnStart) Deploy();
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }

    /// <summary>メニューを展開する</summary>
    private void Deploy()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();

        int count = menuUis.Count;
        for (int i = 0; i < count; i++)
        {
            var ui = menuUis[i];
            float t = count > 1 ? (float)i / (count - 1) : 1f;
            float delay = (count - 1 - i) * stagger; 

            sequence.Insert(delay,
                ui.DOAnchorPos(Vector2.Lerp(startPosition, endPosition, t), duration)
                  .SetEase(deployEase));
        }

        sequence.OnComplete(() =>
        {
            foreach (var ui in menuUis) SetInteractable(ui, true);
        });
    }

    /// <summary>メニューを閉じる(外部から呼び出し可)</summary>
    public void Retract()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();

        foreach (var ui in menuUis)
        {
            SetInteractable(ui, false);
            sequence.Join(ui.DOAnchorPos(startPosition, duration * 0.6f).SetEase(retractEase));
        }

        sequence.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }

    private static void SetInteractable(RectTransform ui, bool value)
    {
        var button = ui.GetComponent<Button>();
        if (button != null) button.interactable = value;
    }
}
