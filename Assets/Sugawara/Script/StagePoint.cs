using UnityEngine;
 
// マップ上のステージアイコン（Stage1〜3 に付ける）
public class StagePoint : MonoBehaviour
{
    public StageData data;   // パネルに出す情報（タイトル・画像・あらすじ）

    [Header("進行")]
    [Tooltip("ステージ番号。ステージごとに違う番号にする（クリア状況の保存に使う）")]
    public int stageId = 1;
    [Tooltip("このステージに行くためにクリアが必要なステージ。空なら最初から行ける")]
    public StagePoint requiredStage;

    [Header("移動")]
    [Tooltip("キャラが立つ位置。空ならこのオブジェクトの位置")]
    public Transform standPoint;

#if UNITY_EDITOR
    [Tooltip("Start → はい で移動するシーン（Project からシーンファイルをドラッグ）")]
    [SerializeField] UnityEditor.SceneAsset scene;
#endif
    [SerializeField, HideInInspector] string sceneName;

    public string SceneName => sceneName;
    public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

    public bool IsCleared => StageProgress.IsCleared(stageId);
    public bool IsUnlocked => requiredStage == null || requiredStage.IsCleared;

#if UNITY_EDITOR
    // ドラッグしたシーンの名前を保存しておく（ビルド後も使えるように）
    void OnValidate()
    {
        sceneName = scene != null ? scene.name : "";
    }
#endif
}