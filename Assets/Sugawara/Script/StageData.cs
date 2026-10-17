using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    public int chapterNumber;          // 第○話
    public string stageTitle;          // ○○○○
    public Sprite stageImage;          // ステージ画像
    [TextArea(2, 4)] public string synopsis; // あらすじ
    public string talkSceneName;       // 遷移先のシーン名
}