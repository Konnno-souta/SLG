using UnityEngine;

namespace Conversation
{
    /// <summary>動作確認用の簡単な追従カメラ。Main Camera に付けて Target にプレイヤーを入れる。</summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        public Transform target;
        [Tooltip("プレイヤーから見たカメラの位置")]
        public Vector3 offset = new Vector3(0f, 4f, -7f);
        [Tooltip("プレイヤーのどこを見るか（足元からの高さ）")]
        public float lookHeight = 1.2f;
        [Tooltip("追いかける速さ（大きいほどピッタリ付いてくる）")]
        public float followSpeed = 8f;

        void LateUpdate()
        {
            if (!target) return;
            Vector3 wanted = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
