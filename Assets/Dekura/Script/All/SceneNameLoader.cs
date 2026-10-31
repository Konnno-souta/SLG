using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNameLoader
{
    /// <summary>
    /// シーンの名前の有無を取得する
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool HasSceneName(string name)
    {
        int count = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (name == System.IO.Path.GetFileNameWithoutExtension(path)) return true;
        }

        return false;
    }
}
