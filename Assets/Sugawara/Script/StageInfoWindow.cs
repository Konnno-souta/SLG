using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 右側のステージ情報ウィンドウ（MenuSlider で横からスライドして出入りする）
// あらすじは CSV から読み込む
public class StageInfoWindow : MonoBehaviour
{
    [SerializeField] MenuSlider slider;   // StageInfoPanel に付けた MenuSlider
    [SerializeField] TMP_Text titleText;
    [SerializeField] Image stageImage;
    [SerializeField] TMP_Text synopsisText;

    [Header("あらすじ CSV")]
    [Tooltip("あらすじの CSV（1行目は id,synopsis）")]
    [SerializeField] TextAsset synopsisCsv;

    StageSynopsisTable synopsisTable;

    void Awake()
    {
        synopsisTable = new StageSynopsisTable(synopsisCsv);
        if (synopsisCsv == null)
            Debug.LogWarning("[StageInfoWindow] Synopsis Csv が設定されていません", this);
    }

    public void Show(StageData d)
    {
        if (d == null)
        {
            Debug.LogWarning("StageData が設定されていません。仮の表示にします");
            if (titleText != null) titleText.text = "未設定";
            if (stageImage != null) stageImage.sprite = null;
            if (synopsisText != null) synopsisText.text = "";
        }
        else
        {
            if (titleText != null) titleText.text = $"第{d.chapterNumber}話　{d.stageTitle}";
            if (stageImage != null) stageImage.sprite = d.stageImage;
            if (synopsisText != null) synopsisText.text = GetSynopsis(d);
        }
        slider.Show();
    }

    public void Hide() => slider.Hide();

    string GetSynopsis(StageData d)
    {
        if (synopsisTable != null && synopsisTable.TryGet(d.SynopsisKey, out string text)) return text;

        Debug.LogWarning($"[StageInfoWindow] あらすじ CSV に id「{d.SynopsisKey}」がありません（{d.name}）", d);
        return "";
    }
}
