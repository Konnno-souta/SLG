using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSVを読み込み、キャラID・クラスIDで引けるようにする。
/// 現在はBattleシーンで利用。将来ほかのシーンと共有する設計に拡張する。
/// </summary>
public sealed class GameDataRepository : MonoBehaviour
{
    [Header("CSVファイルをProjectから割り当てる")]
    [SerializeField] private TextAsset characterCsv;
    [SerializeField] private TextAsset classCsv;

    private readonly Dictionary<int, CharacterData> characters =
        new Dictionary<int, CharacterData>();
    private readonly Dictionary<int, ClassData> classes =
        new Dictionary<int, ClassData>();

    public bool IsLoaded { get; private set; }
    public int CharacterCount => characters.Count;
    public int ClassCount => classes.Count;

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        characters.Clear();
        classes.Clear();
        IsLoaded = false;
        int errorCount = 0;

        if (characterCsv == null || classCsv == null)
        {
            Debug.LogError("GameDataRepository: Character.csvとClass.csvの両方を設定してください。", this);
            return;
        }

        try
        {
            // 先にクラスを読み込む
            foreach (CsvRow row in CsvReader.Read(classCsv, "Class_ID"))
            {
                string id = row.Get("Class_ID");
                if (id == "EnemyClass")
                    continue; // 仮CSVに存在する区切り行

                try
                {
                    ClassData data = ClassData.FromCsv(row);
                    if (data.Id <= 0 || classes.ContainsKey(data.Id))
                        throw new FormatException($"Class_ID「{data.Id}」が不正または重複しています。");
                    classes.Add(data.Id, data);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Debug.LogError($"{classCsv.name} {row.LineNumber}行目: {ex.Message}", this);
                }
            }

            foreach (CsvRow row in CsvReader.Read(characterCsv, "Character_ID"))
            {
                string id = row.Get("Character_ID");
                if (id == "Enemy")
                    continue; // 仮CSVに存在する区切り行

                try
                {
                    CharacterData data = CharacterData.FromCsv(row);
                    if (data.Id <= 0 || characters.ContainsKey(data.Id))
                        throw new FormatException($"Character_ID「{data.Id}」が不正または重複しています。");
                    characters.Add(data.Id, data);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Debug.LogError($"{characterCsv.name} {row.LineNumber}行目: {ex.Message}", this);
                }
            }

            // キャラクターのクラス参照が存在するかを検証
            foreach (CharacterData character in characters.Values)
            {
                if (!classes.ContainsKey(character.LightClassId))
                {
                    errorCount++;
                    Debug.LogError($"Character_ID={character.Id}: " +
                        $"Class_Light={character.LightClassId} がClass.csvにありません。", this);
                }
                if (!classes.ContainsKey(character.DarkClassId))
                {
                    errorCount++;
                    Debug.LogError($"Character_ID={character.Id}: " +
                        $"Class_Dark={character.DarkClassId} がClass.csvにありません。", this);
                }
            }
        }
        catch (Exception ex)
        {
            errorCount++;
            Debug.LogError($"CSV解析エラー: {ex.Message}", this);
        }

        IsLoaded = errorCount == 0 && characters.Count > 0 && classes.Count > 0;
        if (IsLoaded)
            Debug.Log($"CSV読み込み成功：キャラクター {CharacterCount}件 / クラス {ClassCount}件", this);
        else
            Debug.LogError($"CSV読み込み未完了：エラー {errorCount}件。Consoleを確認してください。", this);
    }

    public bool TryGetCharacter(int id, out CharacterData data)
    {
        return characters.TryGetValue(id, out data);
    }

    public bool TryGetClass(int id, out ClassData data)
    {
        return classes.TryGetValue(id, out data);
    }
}
