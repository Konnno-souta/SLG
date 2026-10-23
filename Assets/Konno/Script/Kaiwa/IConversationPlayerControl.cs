using UnityEngine;

namespace Conversation
{
    /// <summary>
    /// 会話中に止めたり、相手の方へ向けたりできるプレイヤー。
    /// ConversationTrigger はこれを持つプレイヤーを自動で操作する。
    /// 自作のプレイヤーでも、これを実装すれば同じように使える。
    /// </summary>
    public interface IConversationPlayerControl
    {
        /// <summary>操作の停止・再開</summary>
        void SetLocked(bool locked);

        /// <summary>指定した位置の方を向く</summary>
        void FaceTowards(Vector3 worldPosition);
    }
}
