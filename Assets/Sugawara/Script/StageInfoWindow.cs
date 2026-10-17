using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 右側のステージ情報ウィンドウ
public class StageInfoWindow : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Image stageImage;
    [SerializeField] TMP_Text synopsisText;

    public void Show(StageData d)
    {
        titleText.text = $"第{d.chapterNumber}話　{d.stageTitle}";
        stageImage.sprite = d.stageImage;
        synopsisText.text = d.synopsis;
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);
}
