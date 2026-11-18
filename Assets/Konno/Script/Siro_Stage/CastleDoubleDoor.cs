using UnityEngine;

/// <summary>
/// 両開きの扉。左右の扉板をちょうつがい(Hinge)を軸に回して開け閉めする。
/// CastleStageGeneratorの「両開きの扉を作成」で作ったプレハブに付く。
/// 使い方(他のスクリプトから): GetComponentInChildren&lt;CastleDoubleDoor&gt;().Open();
/// このファイルはEditorフォルダの外(例: Assets/Konno/Script)に置くこと。
/// </summary>
public sealed class CastleDoubleDoor : MonoBehaviour
{
    [Header("ちょうつがい(自動生成で設定済み)")]
    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;

    [Header("開き方")]
    [SerializeField, Range(0f, 120f)] private float openAngle = 95f;
    [SerializeField, Min(0.01f)] private float openSeconds = 1.2f;
    [Tooltip("オン: 部屋の内側へ開く / オフ: 外側へ開く")]
    [SerializeField] private bool openInward = true;
    [Tooltip("開始時の開き具合(0=閉 1=全開)")]
    [SerializeField, Range(0f, 1f)] private float startOpen = 0f;

    private float leftT, rightT;
    private float leftTarget, rightTarget;

    public bool IsOpen => leftTarget > 0.5f || rightTarget > 0.5f;

    private void Awake()
    {
        leftT = rightT = leftTarget = rightTarget = startOpen;
        Apply();
    }

    private void Update()
    {
        if (Mathf.Approximately(leftT, leftTarget) && Mathf.Approximately(rightT, rightTarget)) return;
        float step = Time.deltaTime / openSeconds;
        leftT = Mathf.MoveTowards(leftT, leftTarget, step);
        rightT = Mathf.MoveTowards(rightT, rightTarget, step);
        Apply();
    }

    /// <summary>両方の扉を開く。</summary>
    public void Open() { leftTarget = 1f; rightTarget = 1f; }

    /// <summary>両方の扉を閉める。</summary>
    public void Close() { leftTarget = 0f; rightTarget = 0f; }

    /// <summary>開いていれば閉め、閉まっていれば開く。</summary>
    public void Toggle() { if (IsOpen) Close(); else Open(); }

    /// <summary>左の扉だけ開け閉めする。</summary>
    public void SetLeft(bool open) => leftTarget = open ? 1f : 0f;

    /// <summary>右の扉だけ開け閉めする。</summary>
    public void SetRight(bool open) => rightTarget = open ? 1f : 0f;

    /// <summary>アニメーションなしで開き具合を決める(0〜1)。</summary>
    public void SetOpenImmediate(float amount)
    {
        leftT = rightT = leftTarget = rightTarget = Mathf.Clamp01(amount);
        Apply();
    }

    private void Apply()
    {
        float dir = openInward ? 1f : -1f;
        if (leftHinge != null)
            leftHinge.localRotation = Quaternion.Euler(0f, -openAngle * Ease(leftT) * dir, 0f);
        if (rightHinge != null)
            rightHinge.localRotation = Quaternion.Euler(0f, openAngle * Ease(rightT) * dir, 0f);
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    /// <summary>自動生成から設定する。</summary>
    public void Setup(Transform left, Transform right)
    {
        leftHinge = left;
        rightHinge = right;
    }

#if UNITY_EDITOR
    // Inspectorで「開始時の開き具合」を動かすと、エディタ上でもすぐ見た目に反映する
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        leftT = rightT = leftTarget = rightTarget = startOpen;
        Apply();
    }
#endif
}
