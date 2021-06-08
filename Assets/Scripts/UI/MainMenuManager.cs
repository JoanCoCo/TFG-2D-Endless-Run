using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private KeyCode playKey = KeyCode.Return;
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    public void PlayPressed()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    public void ExitPressed()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void Update()
    {
        if(Input.GetKeyDown(exitKey))
        {
            ExitPressed();
        }
        else if(Input.GetKeyDown(playKey))
        {
            PlayPressed();
        } 
    }
}
