using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最新版 Character.csv / Class.csv をIDで検索するための窓口。
/// Awakeで両方読み込む（BattleStageLoaderのStartから参照可能）。
/// </summary>
public sealed class GameDataRepository : MonoBehaviour
{
    [Header("プランナーの正式CSVをセット")]
    [SerializeField] private TextAsset characterCsv;
    [SerializeField] private TextAsset classCsv;

    private readonly Dictionary<string, CharacterData> characters =
        new Dictionary<string, CharacterData>(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassData> classes =
        new Dictionary<string, ClassData>(StringComparer.Ordinal);

    public bool IsLoaded { get; private set; }
    public int CharacterCount => characters.Count;
    public int ClassCount => classes.Count;

    private void Awake() => Load();

    public void Load()
    {
        IsLoaded = false;
        characters.Clear();
        classes.Clear();
        int errors = 0;

        if (characterCsv == null || classCsv == null)
        {
            Debug.LogError("GameDataRepository: Character.csv / Class.csv を設定してください。", this);
            return;
        }

        try
        {
            foreach (CsvRow row in CsvReader.Read(classCsv, "CL_ID"))
            {
                string key = row.Get("CL_ID");
                if (key == "EnemyCL") continue; // プランナーCSVの区切り行
                try
                {
                    ClassData data = ClassData.FromCsv(row);
                    if (classes.ContainsKey(data.Id))
                        throw new FormatException($"CL_ID={data.Id} が重複しています。");
                    classes.Add(data.Id, data);
                }
                catch (Exception ex)
                {
                    errors++;
                    Debug.LogError($"{classCsv.name} {row.LineNumber}行目: {ex.Message}", this);
                }
            }
            foreach (CsvRow row in CsvReader.Read(characterCsv, "Character_ID"))
            {
                string key = row.Get("Character_ID");
                if (key == "Enemy") continue; // プランナーCSVの区切り行
                try
                {
                    CharacterData data = CharacterData.FromCsv(row);
                    if (characters.ContainsKey(data.Id))
                        throw new FormatException($"Character_ID={data.Id} が重複しています。");
                    characters.Add(data.Id, data);
                }
                catch (Exception ex)
                {
                    errors++;
                    Debug.LogError($"{characterCsv.name} {row.LineNumber}行目: {ex.Message}", this);
                }
            }

            foreach (CharacterData character in characters.Values)
            {
                if (!classes.ContainsKey(character.LightClassId))
                {
                    errors++;
                    Debug.LogError($"Character_ID={character.Id}: CL_Light={character.LightClassId} がClass.csvにありません。", this);
                }
                if (!classes.ContainsKey(character.DarkClassId))
                {
                    errors++;
                    Debug.LogError($"Character_ID={character.Id}: CL_Dark={character.DarkClassId} がClass.csvにありません。", this);
                }
            }
        }
        catch (Exception ex)
        {
            errors++;
            Debug.LogError($"マスターCSV解析エラー: {ex.Message}", this);
        }

        IsLoaded = errors == 0 && characters.Count > 0 && classes.Count > 0;
        if (IsLoaded)
            Debug.Log($"CSV読み込み成功: キャラクター{CharacterCount}件 / クラス{ClassCount}件", this);
        else
            Debug.LogError($"CSV読み込み失敗: エラー{errors}件。Consoleを確認してください。", this);
    }

    public bool TryGetCharacter(string id, out CharacterData data) =>
        characters.TryGetValue(id ?? "", out data);

    public bool TryGetClass(string id, out ClassData data) =>
        classes.TryGetValue(id ?? "", out data);
}
