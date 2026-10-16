using System.Collections.Generic;
using Title;
using UnityEngine;
using UnityEngine.UI;

namespace Volume
{
    public class Rule 
    {
        public const int MAX_VOLUME = 10;
        public const int MIN_VOLUME = 0;
        public const int DEFAULT_VOLUME = 8;

        public const string MASTER = "MASTER";
        public const string BGM = "BGM";
        public const string SE = "SE";
    }

    public static class VolumeData
    {
        private static readonly Dictionary<string, int> levels = new();

        public static int Get(string key) =>
            levels.TryGetValue(key, out int v)
                ? v
                : (levels[key] = PlayerPrefs.GetInt(key, Rule.DEFAULT_VOLUME));

        public static void Set(string key, int level)
        {
            levels[key] = level;
            PlayerPrefs.SetInt(key, level);
        }
    }

    public class VolumeSetting : MonoBehaviour,ICloseButton
    {
        [SerializeField] private Volume_Row volume_MASTER;
        [SerializeField] private Volume_Row volume_BGM;
        [SerializeField] private Volume_Row volume_SE;

        [SerializeField] private Button closeButton;

        //-------------------------------------------------------------------------------------//

        private void Start()
        {
            volume_MASTER.onChanged += vol => VolumeChange(Rule.MASTER, vol);
            volume_BGM.onChanged += vol => VolumeChange(Rule.BGM, vol);
            volume_SE.onChanged += vol => VolumeChange(Rule.SE, vol);

            volume_MASTER.InitData(VolumeData.Get(Rule.MASTER));
            volume_BGM.InitData(VolumeData.Get(Rule.BGM));
            volume_SE.InitData(VolumeData.Get(Rule.SE));
        }

        public void InitCloseButton(Title_ButtonAction act)
        {
            closeButton.onClick.AddListener(() => act.Close());
        }

        private void VolumeChange(string name, int vol)
        {
            VolumeData.Set(name, vol);
        }
    }
}

