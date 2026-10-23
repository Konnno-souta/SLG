using UnityEngine;

/// <summary>開発用のデータ確認。正常に確認できたらコンポーネントを外してよい。</summary>
public sealed class CsvLoadTest : MonoBehaviour
{
    [SerializeField] private GameDataRepository repository;

    private void Start()
    {
        if (repository == null || !repository.IsLoaded)
        {
            Debug.LogError("CsvLoadTest: GameDataRepositoryの読み込みに失敗しています。", this);
            return;
        }
        ShowCharacter("Chara01");
        ShowCharacter("Chara14");
    }

    private void ShowCharacter(string id)
    {
        if (!repository.TryGetCharacter(id, out CharacterData character))
        {
            Debug.LogWarning($"Character_ID={id} が見つかりません。", this);
            return;
        }
        repository.TryGetClass(character.LightClassId, out ClassData light);
        repository.TryGetClass(character.DarkClassId, out ClassData dark);

        string lightName = light != null ? light.Name : "参照エラー";
        string darkName = dark != null ? dark.Name : "参照エラー";
        string attackPreview = light != null
            ? (character.BaseAttack * light.AttackRatio).ToString("0.##")
            : "参照エラー";

        Debug.Log($"[{id}] {character.Name} Team={character.Team} HP_Basic={character.BaseHP} " +
            $"ATK_Basic={character.BaseAttack} 朝={lightName} 夜={darkName} " +
            $"朝ATK参考値(丸め前)={attackPreview}", this);
    }
}
