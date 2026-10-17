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
        //セーブデータ読み込み(実装後)
        if (savedata) isSaved = true;

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
}
