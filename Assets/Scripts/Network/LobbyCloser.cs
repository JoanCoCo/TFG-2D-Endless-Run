using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI;

public class LobbyCloser : NetworkBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private GameObject netManagerObject;
    [SerializeField] private string scene;

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        Debug.Log("Interacting with LobbyCloser");
        if(NetworkManager.Singleton != null)
        {
            if(IsServer)
            {
                Debug.Log("Server LobbyCloser.");
                GameObject.FindWithTag("PlayersManager").GetComponent<PlayersManager>().IsolateHost(this);
                //netManager.StopHost();
                //Close();
            } else
            {
                Debug.Log("Client LobbyCloser.");
                GameObject.FindWithTag("PlayersManager").GetComponent<PlayersManager>().IsolateClient(this);
                //netManager.StopClient();
            }
        }
    }

    public void Close()
    {
        Destroy(netManagerObject);
        //NetworkManager.networkSceneName = "";
        //NetworkManager.Singleton.Shutdown();
        //NetworkTransport.Shutdown();
        GameObject playersManager = GameObject.FindWithTag("PlayersManager");
        if (playersManager != null) Destroy(playersManager);
        GameObject playersFinder = GameObject.FindWithTag("PlayersFinder");
        if (playersFinder != null) Destroy(playersFinder);
        GameObject player = GameObject.FindWithTag("LocalPlayer");
        if (player != null) Destroy(player);
        SceneManager.LoadScene(scene);
    }
}
