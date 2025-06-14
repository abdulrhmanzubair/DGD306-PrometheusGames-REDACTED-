using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex+1);
    }
    public void Guide ()
    {
        SceneManager.LoadScene("Guide");
    }
    public void Quit()
    {
        Application.Quit();
    }

}
