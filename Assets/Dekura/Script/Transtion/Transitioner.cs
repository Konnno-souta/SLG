using UnityEngine;
using System.Collections;

namespace Transition
{
    public class Transitioner : MonoBehaviour
    {
        [SerializeField] private float transitionInterval = 2f;
        [SerializeField] private GameObject transitionObj;

        string standbyScene;

        public void Transition(string nextscene)
        {
            if (standbyScene != null) return;
            if (!SceneNameLoader.HasSceneName(nextscene))
            {
                Debug.Log("Scene‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ!!!!!!!!!!!!!!!!!!!!");
                return;
            }

            ScreenFader.FadeOutIn(0.5f, 0.5f, () =>
            {
                GameObject go = Instantiate(transitionObj);
                go.GetComponent<TransitionAnimation>()?.InitData(transitionInterval,this);
            });

            standbyScene = nextscene;
        }

        public IEnumerator Transitioning()
        {
            yield return new WaitForSeconds(transitionInterval);

            ScreenFader.LoadScene(standbyScene);
            standbyScene = null;
        }
    }
}

