using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MediaCommunicationManager : MonoBehaviour
{
    [SerializeField] private MediaInputController camController;
    [SerializeField] private MediaInputController micController;
    private bool searching = false;

    private string lastScene;

    private void Start()
    {
        StartCoroutine(FindPlayer());
        lastScene = GetCurrentlyActiveSceneName();
        Messenger<string>.AddListener(NetworkEvent.SPLIT, OnSplit);
    }

    private void OnSplit(string s)
    {
        camController.SetMedia(null);
        micController.SetMedia(null);
    }

    private string GetCurrentlyActiveSceneName() => SceneManager.GetActiveScene().name;

    private void Update()
    {
        if(!searching && (!lastScene.Equals(GetCurrentlyActiveSceneName()) || camController.IsNotSet() || micController.IsNotSet()))
        {
            lastScene = GetCurrentlyActiveSceneName();
            StartCoroutine(FindPlayer());
        }
    }

    IEnumerator FindPlayer()
    {
        searching = true;
        GameObject localPlayer = GameObject.FindWithTag("LocalPlayer");
        while(localPlayer == null)
        {
            yield return new WaitForSeconds(0.01f);
            localPlayer = GameObject.FindWithTag("LocalPlayer");
        }
        camController.SetMedia(localPlayer.GetComponent<CamManager>());
        micController.SetMedia(localPlayer.GetComponent<MicManager>());
        searching = false;
    }

    private void OnDestroy()
    {
        Messenger<string>.RemoveListener(NetworkEvent.SPLIT, OnSplit);
    }
}
