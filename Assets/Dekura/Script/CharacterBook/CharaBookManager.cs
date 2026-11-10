using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class CharaBookManager : MonoBehaviour
{
    [Header("CSV")]
    [SerializeField] private TextAsset csv;
    [SerializeField] private string charaPass = "Assets/Dekura/Material/Image/@CharacterBook/Charas/";

    [Header("LOOK")]
    [SerializeField] private Image charaImage;
    [SerializeField] private TextMeshProUGUI charaSprites_Name;
    [SerializeField] private TextMeshProUGUI charaSprites_Disc;

    [Header("PageFlipper")]
    [SerializeField] private PageFlipEffect flip;

    [Header("DEFAULT")]
    [SerializeField][Min(1)] private int default_Pages = 1;

    //-------------------------------------------------------------------------------------//

    AsyncOperationHandle<Sprite> charaHandle;
    private int nowPage = 1;

    //-------------------------------------------------------------------------------------//

    private void Awake()
    {
        nowPage = default_Pages;
        UpdateLooks();
    }

    private void Update()
    {
        gameObject.GetComponent<CanvasGroup>().alpha = flip.IsPlaying ? 0f : 1f;

        //仮置き//
        //あとでしおり8つで動くようにする//
        if (Keyboard.current != null && Keyboard.current[Key.RightArrow].isPressed)
        {
            if (nowPage < 8)
            {
                flip.Play(PageFlipEffect.FlipDirection.ToLeft, 10, 0.35f, 0.06f);
                nowPage++;
                UpdateLooks();
            }
        }
        else if (Keyboard.current != null && Keyboard.current[Key.LeftArrow].isPressed)
        {
            if (nowPage > 1)
            {
                flip.Play(PageFlipEffect.FlipDirection.ToRight, 10, 0.35f, 0.06f);
                nowPage--;
                UpdateLooks();
            }
        }
        //-----//
    }

    private void UpdateLooks()
    {
        foreach (CsvRow row in CsvReader.Read(csv, "BOOK_ID"))
        {
            if(row.GetInt("BOOK_ID") != nowPage) continue;

            LoadCharaImage(row.Get("Chara_Name"), (sprite) =>
            {
                charaImage.enabled = true;
                charaImage.sprite = sprite; 
                charaImage.SetNativeSize();
            });
            charaSprites_Name.text = row.Get("Chara_Name");
            charaSprites_Disc.text = row.Get("Chara_Disc");
            break;
        }
    }

    private void LoadCharaImage(string charaID, Action<Sprite> onLoaded)
    {
        if (charaHandle.IsValid()) Addressables.Release(charaHandle);
        charaHandle = Addressables.LoadAssetAsync<Sprite>(charaPass + charaID + ".png");

        charaHandle.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    onLoaded?.Invoke(handle.Result);
                }
                else
                {
                    Debug.LogError($"画像の読み込み失敗: {charaPass + charaID + ".png"}");
                }
            };
    }

    void OnDestroy()
    {
        if (charaHandle.IsValid()) Addressables.Release(charaHandle);
    }
}
