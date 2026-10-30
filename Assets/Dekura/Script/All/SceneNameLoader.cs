using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNameLoader
{
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
