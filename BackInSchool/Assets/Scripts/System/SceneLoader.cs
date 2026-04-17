using UnityEngine;

public class SceneLoader : MonoBehaviour
{
    public void LoadSceneByName(string sceneName)
    {
        SceneTransitionFader.LoadSceneWithFade(sceneName);
    }

    public void LoadSchoolFreeTime()
    {
        SceneTransitionFader.LoadSceneWithFade("FREEROAM");
    }

    public void LoadSubway()
    {
        SceneTransitionFader.LoadSceneWithFade("CHAT");
    }

    public void Story()
    {
        SceneTransitionFader.LoadSceneWithFade("STORY");
    }

    public void MINIGAME()
    {
        SceneTransitionFader.LoadSceneWithFade("MINIGAME");
    }

    public void LoadMainMenu()
    {
        SceneTransitionFader.LoadSceneWithFade("MainMenu");
    }
}
