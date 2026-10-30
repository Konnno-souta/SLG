using UnityEngine;
using System.Collections;

public class Transitioner : MonoBehaviour
{
    [SerializeField] private float transitionInterval = 2f;
    [SerializeField] private GameObject transitionObj;

    public void Transition(string nextscene)
    {
        if (!SceneNameLoader.HasSceneName(nextscene))
        {
            Debug.Log("Scene‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ!!!!!!!!!!!!!!!!!!!!");
            return;
        }

        ScreenFader.FadeOutIn(0.5f, 0.5f, () => Transitioning(nextscene));
    }

    private IEnumerator Transitioning(string nextscene)
    {
        Instantiate(transitionObj);
        yield return new WaitForSeconds(transitionInterval);
        ScreenFader.LoadScene(nextscene);
    }
}
