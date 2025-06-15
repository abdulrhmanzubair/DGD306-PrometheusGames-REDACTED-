// Back.cs - Fixed version
using UnityEngine;
using UnityEngine.SceneManagement; // ✅ Correct namespace - REMOVED UnityEditor.SceneManagement
using System;

public class Back : MonoBehaviour
{
    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
