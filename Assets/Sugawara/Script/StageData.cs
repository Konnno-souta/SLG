using UnityEngine;

// ステージ1つ分の情報（Projectで右クリック > Create > Game > StageData で作成）
// あらすじは CSV で管理する（StageInfoWindow の Synopsis Csv に入れた CSV から読む）
[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    public int chapterNumber;          // 第○話
    public string stageTitle;          // ○○○○
    public Sprite stageImage;          // ステージ画像

    [Tooltip("あらすじ CSV の id 列の値。空なら Chapter Number を id として使う")]
    public string synopsisId;

    // CSV で探すときの id
    public string SynopsisKey => string.IsNullOrEmpty(synopsisId) ? chapterNumber.ToString() : synopsisId;
}