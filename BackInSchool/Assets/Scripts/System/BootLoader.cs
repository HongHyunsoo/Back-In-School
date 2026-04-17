using UnityEngine;

public class BootLoader : MonoBehaviour
{
    private void Start()
    {
        SceneTransitionFader.LoadSceneWithFade("MainMenu");
    }
}
