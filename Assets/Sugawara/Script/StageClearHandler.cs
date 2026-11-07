using UnityEngine;
using UnityEngine.SceneManagement;

// ゲームプレイシーンに置いておき、クリアしたときに StageClear() を呼ぶ
public class StageClearHandler : MonoBehaviour
{
    [SerializeField] string mapSceneName = "StageSelectScene";

    public void StageClear()
    {
        StageProgress.ClearCurrentStage();
        SceneManager.LoadScene(mapSceneName);
    }
}