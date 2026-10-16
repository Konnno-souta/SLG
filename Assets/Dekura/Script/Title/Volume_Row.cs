using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Volume
{
    public class Volume_Row : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI volumeText;

        [Header("ボタン")]
        [SerializeField] private Button volumeChange_up;
        [SerializeField] private Button volumeChange_down;

        [Header("音量チップ")]
        [SerializeField] private GameObject chipArea;
        [SerializeField] private Color volumeChipColor_enable;
        [SerializeField] private Color volumeChipColor_disable;

        public event System.Action<int> onChanged;

        //-------------------------------------------------------------------------------------//

        private List<Image> volumeChips = new();
        private int nowVolume = 0;

        private void Awake()
        {
            volumeChange_up.onClick.AddListener(() => VolumeChange(+1));
            volumeChange_down.onClick.AddListener(() => VolumeChange(-1));

            volumeChips.AddRange(chipArea.GetComponentsInChildren<Image>());

            UpdateUi();
        }

        public void InitData(int vol)
        {
            nowVolume = vol;
            UpdateUi();
        }

        private void UpdateUi()
        {
            volumeText.text = $"{nowVolume}";

            for (int i = Rule.MIN_VOLUME; i < Rule.MAX_VOLUME; i++)
            {
                volumeChips[i].color = i < nowVolume
                    ? volumeChips[i].color = volumeChipColor_enable
                    : volumeChips[i].color = volumeChipColor_disable;
            }
        }

        public void VolumeChange(int mode)
        {
            nowVolume = Mathf.Min(Rule.MAX_VOLUME, nowVolume + mode);
            nowVolume = Mathf.Max(Rule.MIN_VOLUME, nowVolume);

            onChanged?.Invoke(nowVolume);
            UpdateUi();
        }
    }
}

