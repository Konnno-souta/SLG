using UnityEngine;

/// <summary>
/// CSV読み込み結果をConsoleへ出す開発用テスト。
/// 確認できたらコンポーネントを外してよい。
/// </summary>
public sealed class CsvLoadTest : MonoBehaviour
{
    [SerializeField] private GameDataRepository repository;

    private void Start()
    {
        if (repository == null || !repository.IsLoaded)
        {
            Debug.LogError("CsvLoadTest: GameDataRepositoryの設定またはCSV読み込みを確認してください。", this);
            return;
        }

        ShowCharacter(1);  // クラン
        ShowCharacter(14); // 敵兵1
    }

    private void ShowCharacter(int id)
    {
        if (!repository.TryGetCharacter(id, out CharacterData character))
        {
            Debug.LogWarning($"Character_ID={id} が見つかりません。", this);
            return;
        }

        string lightName = "見つからない";
        string darkName = "見つからない";

        if (repository.TryGetClass(character.LightClassId, out ClassData light))
            lightName = light.Name;
        if (repository.TryGetClass(character.DarkClassId, out ClassData dark))
            darkName = dark.Name;

        Debug.Log(
            $"[Character_ID={character.Id}] {character.Name} " +
            $"所属ID={character.PositionId} " +
            $"HP={character.BaseHP} ATK={character.BaseAttack} " +
            $"昼={lightName}(ID:{character.LightClassId}) " +
            $"夜={darkName}(ID:{character.DarkClassId})",
            this);
    }
}
