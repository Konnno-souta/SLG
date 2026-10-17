using Title;
using UnityEngine;
using UnityEngine.UI;

public class SaveDataSelect : MonoBehaviour,ICloseButton
{
    [SerializeField] private Button closeButton;

    public void InitCloseButton(Title_ButtonAction act) => closeButton.onClick.AddListener(() => act.Close());
}
