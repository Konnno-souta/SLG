using System;
using System.Collections.Generic;
using UnityEngine;

namespace Conversation
{
    [Serializable]
    public class CharacterExpression
    {
        [Tooltip("表情名（スクリプトで指定する名前。例: normal, smile）")]
        public string name = "normal";
        [Tooltip("立ち絵")]
        public Sprite sprite;
        [Tooltip("ログ用の顔アイコン（空なら立ち絵の顔部分を自動で切り抜く）")]
        public Sprite logIcon;
    }

    [Serializable]
    public class CharacterProfile
    {
        [Tooltip("スクリプトで使うID（半角英数字推奨。例: yuzuki）")]
        public string id;
        [Tooltip("表示名（例: 星野ユズキ）")]
        public string displayName;
        [Tooltip("名前の横に出る所属（例: 生徒会）")]
        public string affiliation;

        [Tooltip("1つ目がデフォルトの表情になる")]
        public List<CharacterExpression> expressions = new List<CharacterExpression>();

        [Header("立ち絵")]
        [Tooltip("立ち絵の大きさ倍率")]
        public float scale = 1f;
        [Tooltip("立ち絵の位置ずれ補正 (px)")]
        public Vector2 offset;
        [Tooltip("エモート（!? など）を出す位置。立ち絵内の割合 (0〜1)")]
        public Vector2 emoteAnchor = new Vector2(0.68f, 0.88f);

        [Header("ログの顔アイコン（自動切り抜き時）")]
        [Tooltip("立ち絵の中で顔の中心がある位置（0〜1。左下が0,0）")]
        public Vector2 faceCenter = new Vector2(0.5f, 0.82f);
        [Tooltip("切り抜きの拡大率。大きいほど顔がアップになる")]
        public float logIconZoom = 3.2f;

        public CharacterExpression FindExpression(string expression)
        {
            if (expressions.Count == 0) return null;
            if (string.IsNullOrEmpty(expression)) return expressions[0];

            foreach (var e in expressions)
                if (string.Equals(e.name, expression, StringComparison.OrdinalIgnoreCase))
                    return e;

            Debug.LogWarning($"[Conversation] {id} に表情 '{expression}' がありません");
            return expressions[0];
        }

        public Sprite GetSprite(string expression) => FindExpression(expression)?.sprite;
    }

    [Serializable]
    public class BackgroundEntry
    {
        public string name;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Conversation/Database", fileName = "ConversationDatabase")]
    public class ConversationDatabase : ScriptableObject
    {
        public List<CharacterProfile> characters = new List<CharacterProfile>();
        public List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

        public CharacterProfile FindCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in characters)
                if (string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }

        public Sprite FindBackground(string name)
        {
            foreach (var b in backgrounds)
                if (string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase))
                    return b.sprite;
            return null;
        }
    }
}
