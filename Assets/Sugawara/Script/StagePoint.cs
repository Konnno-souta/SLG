using UnityEngine;

// マップ上のステージアイコン（Stage1〜3 に付ける）
public class StagePoint : MonoBehaviour
{
    public StageData data;   // パネルに出す情報（タイトル・画像・あらすじ）

    [Tooltip("キャラが立つ位置。空ならこのオブジェクトの位置")]
    public Transform standPoint;

#if UNITY_EDITOR
    [Tooltip("Start → はい で移動するシーン（Project からシーンファイルをドラッグ）")]
    [SerializeField] UnityEditor.SceneAsset scene;
#endif
    [SerializeField, HideInInspector] string sceneName;

    public string SceneName => sceneName;
    public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

#if UNITY_EDITOR
    // ドラッグしたシーンの名前を保存しておく（ビルド後も使えるように）
    void OnValidate()
    {
        sceneName = scene != null ? scene.name : "";
    }
#endif
}