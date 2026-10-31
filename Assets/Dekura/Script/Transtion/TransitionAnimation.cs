using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

namespace Transition
{
    public class TransitionAnimation : MonoBehaviour
    {
        [Header("Loadチップ")]
        public TextMeshProUGUI tipsText;
        public GameObject chipArea;
        public Color loadChipColor_enable;
        public Color loadChipColor_disable;

        [Header("CSV")]
        public TextAsset csv;

        [Header("StageId(どっかで取得するんだろうナー)")]
        public int nowStage = 1;

        //-----------------------------------------

        Dictionary<int, string> tipTexts = new Dictionary<int, string>();
        List<Image> loadChips = new();

        Transitioner transitioner;
        float loadMinTime = 2f;
        float timer;

        bool isLoaded = false;

        void Awake()
        {
            foreach (CsvRow row in CsvReader.Read(csv, "Story_ID"))
            {
                int id = row.GetInt("Story_ID");
                string text = row.Get("Text");

                if (id > nowStage) continue;

                tipTexts.Add(id, text);
            }


            loadChips.AddRange(chipArea.GetComponentsInChildren<Image>());
            TipsUpdate();
        }

        public void InitData(float loadTime, Transitioner tr)
        {
            loadMinTime = loadTime;
            transitioner = tr;
        }

        void FixedUpdate()
        {
            if (isLoaded) return;

            LoadChipUpdate();
            if (Mouse.current.leftButton.wasPressedThisFrame) TipsUpdate();
        }

        void TipsUpdate()
        {
            if (tipTexts == null) return;


            tipsText.text = tipTexts[1];
        }

        void LoadChipUpdate()
        {
            timer += Time.deltaTime;

            for (int i = 0; i < loadChips.Count; i++)
            {
                loadChips[i].color = loadMinTime * i * 0.1 < timer
                    ? loadChips[i].color = loadChipColor_enable
                    : loadChips[i].color = loadChipColor_disable;
            }

            if (loadChips[loadChips.Count - 1].color == loadChipColor_enable)
            {
                transitioner?.Transitioning();
                isLoaded = true;
            }
        }
    }
}
