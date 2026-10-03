using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{
    // Button 1: Enter Game
    public void EnterGame()
    {
        SceneManager.LoadScene(1);
    }

    // Button 2: Exit Game
    public void ExitGame()
    {
        Application.Quit();
       
    }
}
