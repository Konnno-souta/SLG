
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveDataSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI playTime;
    [SerializeField] private TextMeshProUGUI playStage_Floor;

    [SerializeField] private GameObject dataArea;
    [SerializeField] private GameObject noDataArea;

    [SerializeField] private Button closeButton;

#if UNITY_EDITOR
    [SerializeField] private bool savedata;
#endif

    //-------------------------------------------------//

    private bool isSaved = false;

    private void Awake()
    {
#if UNITY_EDITOR
        if (savedata) isSaved = true;
#endif

        UpdateUi();
    }

    private void UpdateUi()
    {
        if (isSaved)
        {
            dataArea.SetActive(true);
            noDataArea.SetActive(false);
        }
        else
        {
            dataArea.SetActive(false);
            noDataArea.SetActive(true);
        }
    }


    /// <summary>
    /// 外部スクリプトからJSONの保存状態を受け取る。
    ///
    /// 実際のJSON読み込みは行わず、
    /// 既存の表示処理だけを呼び出す。
    /// </summary>
    public void SetSaveInfo(
        bool hasData,
        string stageId)
    {
        isSaved = hasData;

        if (isSaved)
        {
            // 現在のセーブJSONには
            // プレイヤー名・累計プレイ時間がない
            if (playerName != null)
            {
                playerName.text = "名前未記録";
            }

            if (playTime != null)
            {
                playTime.text = "TIME: --:--";
            }

            if (playStage_Floor != null)
            {
                playStage_Floor.text =
                    "STAGE: " + stageId;
            }
        }

        UpdateUi();
    }
}
