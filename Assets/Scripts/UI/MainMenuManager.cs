using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private KeyCode playKey = KeyCode.Return;
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;
    private InputAvailabilityManager inputAvailabilityManager;

    public void PlayPressed()
    {
        if (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping)
        {
            SceneManager.LoadScene("LobbyScene");
        }
    }

    public void ExitPressed()
    {
        if (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void Start()
    {
        var o = GameObject.FindWithTag("InputAvailabilityManager");
        if (o != null) inputAvailabilityManager = o.GetComponent<InputAvailabilityManager>();
        MessengerInternal.DEFAULT_MODE = MessengerMode.DONT_REQUIRE_LISTENER;
    }

    private void Update()
    {
        if (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping)
        {
            if (Input.GetKeyDown(exitKey))
            {
                ExitPressed();
            }
            else if (Input.GetKeyDown(playKey))
            {
                PlayPressed();
            }
        }
    }
}
