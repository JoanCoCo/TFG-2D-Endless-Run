using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MediaCommunicationManager : MonoBehaviour
{
    [SerializeField] private MediaInputController camController;
    [SerializeField] private MediaInputController micController;

    private string lastScene;

    private void Start()
    {
        StartCoroutine(FindPlayer());
        lastScene = GetCurrentlyActiveSceneName();
    }

    private string GetCurrentlyActiveSceneName() => SceneManager.GetActiveScene().name;

    private void Update()
    {
        if(!lastScene.Equals(GetCurrentlyActiveSceneName()))
        {
            lastScene = GetCurrentlyActiveSceneName();
            StartCoroutine(FindPlayer());
        }
    }

    IEnumerator FindPlayer()
    {
        GameObject localPlayer = GameObject.FindWithTag("LocalPlayer");
        while(localPlayer == null)
        {
            yield return new WaitForSeconds(0.01f);
            localPlayer = GameObject.FindWithTag("LocalPlayer");
        }
        camController.SetMedia(localPlayer.GetComponent<CamManager>());
        micController.SetMedia(localPlayer.GetComponent<MicManager>());
    }
}
