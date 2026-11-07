using System.Collections;
using Transition;
using UnityEngine;

public class Transitioner : MonoBehaviour
{
    public static Transitioner Instance { get; private set; }

    [SerializeField] private float transitionInterval = 2f;
    [SerializeField] private GameObject transitionObj;

    [Header("DEBUG")]
    [SerializeField] private bool testmode = false;
    string standbyScene;

    private void Awake()
    {
        //------インスタンス化------//
        if (Instance != null)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        //---------------------------//
        if (testmode) Transition("MapScene");
    }

    public void Transition(string nextscene)
    {
        if (standbyScene != null) return;
        if (!SceneNameLoader.HasSceneName(nextscene))
        {
            Debug.Log("Sceneが見つかりません!!!!!!!!!!!!!!!!!!!!");
            return;
        }

        ScreenFader.FadeOutIn(1f, 0.5f, () =>
        {
            GameObject go = Instantiate(transitionObj);
            go.GetComponent<TransitionAnimation>()?.InitData(transitionInterval, this);
        });

        standbyScene = nextscene;
    }

    public void Transitioning()
    {
        ScreenFader.LoadScene(standbyScene, 1f);
        standbyScene = null;
    }
}

