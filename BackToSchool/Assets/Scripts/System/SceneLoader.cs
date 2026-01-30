using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // 버튼에서 문자열로 씬 이름 넣어서 호출 가능
    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Subway로 고정 이동 버튼용 (인자 없이도 가능)
    public void LoadSchoolFreeTime()
    {
        SceneManager.LoadScene("SchoolFreeTime");
    } 
    
    public void LoadSubway()
    {
        SceneManager.LoadScene("SubwayScene");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
