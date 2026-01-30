using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    [SerializeField] private string firstSceneName = "MainMenu";

    private void Start()
    {
        SceneManager.LoadScene(firstSceneName);
    }
}
