using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フィールド(StageConfig の 3D_Name)ごとの「マップチップ」の見た目の定義。
/// タイル配置CSVの値(1=壁、2=障害物 など)ごとに、置くプレハブを決める。
/// 作り方: Project で右クリック > Create > SLG > マップチップセット
/// (城は Castle Stage Generator の「城のマップチップを作る」で自動作成できる)
/// ※このファイルは Editor フォルダに入れないこと。
/// </summary>
[CreateAssetMenu(fileName = "TileChipSet", menuName = "SLG/マップチップセット")]
public sealed class TileChipSet : ScriptableObject
{
    [Serializable]
    public sealed class Chip
    {
        [Tooltip("タイル配置CSVの値")]
        public int value = 1;

        [Tooltip("メモ(壁、障害物など)")]
        public string note = "";

        [Tooltip("置くプレハブ。原点=マスの中心・地面の高さ。空なら仮の箱を置く")]
        public GameObject prefab;

        [Tooltip("90度単位でランダムに回す(岩や木など向きのないもの向け)")]
        public bool randomRotate;
    }

    [Tooltip("使うフィールド名(StageConfig.csv の 3D_Name。例: 3D10)")]
    [SerializeField] private string fieldName = "3D10";

    [Tooltip("チップを作ったときの1マスの大きさ(m)。BattleGridのCell Sizeと違えば自動で拡大縮小")]
    [SerializeField, Min(0.01f)] private float chipCellSize = 1f;

    [SerializeField] private List<Chip> chips = new List<Chip>();

    public string FieldName => fieldName;
    public float ChipCellSize => chipCellSize;

    public Chip Find(int value) => chips.Find(c => c != null && c.value == value);

#if UNITY_EDITOR
    /// <summary>エディタ拡張から中身を設定する。</summary>
    public void EditorSet(string field, float cellSize, List<Chip> list)
    {
        fieldName = field;
        chipCellSize = cellSize;
        chips = list;
    }
#endif
}
