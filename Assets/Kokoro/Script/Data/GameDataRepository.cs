
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character.csv / Class.csvを読み込み、
/// IDからデータを検索する。
///
/// 第18段階：新CSVの数値ID形式に対応。
///
/// Awakeで読み込むため、既存の
/// BattleStageLoader.Startから利用可能。
/// </summary>
public sealed class GameDataRepository : MonoBehaviour
{
    [Header("プランナーの正式CSVをセット")]
    [SerializeField]
    private TextAsset characterCsv;

    [SerializeField]
    private TextAsset classCsv;

    // ========================================
    // マスターデータ
    // ========================================

    private readonly Dictionary<string, CharacterData>
        characters =
            new Dictionary<string, CharacterData>(
                StringComparer.Ordinal
            );

    private readonly Dictionary<string, ClassData>
        classes =
            new Dictionary<string, ClassData>(
                StringComparer.Ordinal
            );

    public bool IsLoaded { get; private set; }

    public int CharacterCount => characters.Count;
    public int ClassCount => classes.Count;

    // ========================================
    // 初期化
    // ========================================

    private void Awake()
    {
        Load();
    }

    // ========================================
    // CSV読み込み
    // ========================================

    public void Load()
    {
        IsLoaded = false;

        characters.Clear();
        classes.Clear();

        int errors = 0;

        if (characterCsv == null ||
            classCsv == null)
        {
            Debug.LogError(
                "GameDataRepository: " +
                "Character.csv / Class.csvを" +
                "設定してください。",
                this
            );

            return;
        }

        try
        {
            // =================================
            // クラスを先に読み込む
            // =================================

            foreach (CsvRow row in CsvReader.Read(
                classCsv,
                "CL_ID"))
            {
                string key = row.Get("CL_ID");

                // Excelの説明・計算用行など、
                // IDが空の行は登録しない。
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // 以前のCSVで使用していた
                // 区切り行への互換処理も残す。
                if (key == "EnemyCL")
                {
                    continue;
                }

                try
                {
                    ClassData data =
                        ClassData.FromCsv(row);

                    if (classes.ContainsKey(data.Id))
                    {
                        throw new FormatException(
                            $"CL_ID={data.Id} が" +
                            "重複しています。"
                        );
                    }

                    classes.Add(
                        data.Id,
                        data
                    );
                }
                catch (Exception ex)
                {
                    errors++;

                    Debug.LogError(
                        $"{classCsv.name} " +
                        $"{row.LineNumber}行目: " +
                        ex.Message,
                        this
                    );
                }
            }

            // =================================
            // キャラクターを読み込む
            // =================================

            foreach (CsvRow row in CsvReader.Read(
                characterCsv,
                "Character_ID"))
            {
                string key =
                    row.Get("Character_ID");

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // 旧CSVの区切り行への対応。
                if (key == "Enemy")
                {
                    continue;
                }

                try
                {
                    CharacterData data =
                        CharacterData.FromCsv(row);

                    if (characters.ContainsKey(
                        data.Id))
                    {
                        throw new FormatException(
                            $"Character_ID={data.Id} が" +
                            "重複しています。"
                        );
                    }

                    characters.Add(
                        data.Id,
                        data
                    );
                }
                catch (Exception ex)
                {
                    errors++;

                    Debug.LogError(
                        $"{characterCsv.name} " +
                        $"{row.LineNumber}行目: " +
                        ex.Message,
                        this
                    );
                }
            }

            // =================================
            // クラスIDの存在確認
            // =================================

            foreach (CharacterData character
                in characters.Values)
            {
                if (!classes.ContainsKey(
                    character.LightClassId))
                {
                    errors++;

                    Debug.LogError(
                        $"Character_ID={character.Id}: " +
                        $"CL_Light=" +
                        $"{character.LightClassId} が" +
                        "Class.csvにありません。",
                        this
                    );
                }

                if (!classes.ContainsKey(
                    character.DarkClassId))
                {
                    errors++;

                    Debug.LogError(
                        $"Character_ID={character.Id}: " +
                        $"CL_Dark=" +
                        $"{character.DarkClassId} が" +
                        "Class.csvにありません。",
                        this
                    );
                }
            }
        }
        catch (Exception ex)
        {
            errors++;

            Debug.LogError(
                "マスターCSV解析エラー: " +
                ex.Message,
                this
            );
        }

        // ====================================
        // 読み込み結果
        // ====================================

        IsLoaded =
            errors == 0 &&
            characters.Count > 0 &&
            classes.Count > 0;

        if (IsLoaded)
        {
            Debug.Log(
                "CSV読み込み成功: " +
                $"キャラクター{CharacterCount}件 / " +
                $"クラス{ClassCount}件",
                this
            );
        }
        else
        {
            Debug.LogError(
                "CSV読み込み失敗: " +
                $"エラー{errors}件。" +
                "Consoleを確認してください。",
                this
            );
        }
    }

    // ========================================
    // キャラクター検索
    // ========================================

    public bool TryGetCharacter(
        string id,
        out CharacterData data)
    {
        return characters.TryGetValue(
            id ?? "",
            out data
        );
    }

    // ========================================
    // クラス検索
    // ========================================

    public bool TryGetClass(
        string id,
        out ClassData data)
    {
        return classes.TryGetValue(
            id ?? "",
            out data
        );
    }
}
