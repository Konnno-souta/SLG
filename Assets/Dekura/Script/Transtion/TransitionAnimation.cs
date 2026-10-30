using TMPro;
using UnityEngine;

namespace Transition
{
    public class TransitionAnimation : MonoBehaviour
    {
        [Header("Option")]
        public float loadMinTime=1f;

        [Header("")]
        public TextMeshProUGUI tipsText;
        public GameObject chipArea;
        public Color loadChipColor_enable;
        public Color loadChipColor_disable;



        float timer;
    }
}
