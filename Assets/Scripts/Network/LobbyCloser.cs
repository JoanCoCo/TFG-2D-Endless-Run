using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LobbyCloser : NetworkBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private GameObject netManagerObject;
    private NetworkManager netManager;
    [SerializeField] private string scene;

    private void Start()
    {
        if (netManagerObject == null) netManagerObject = GameObject.FindWithTag("NetManager");
        netManager = netManagerObject.GetComponent<NetworkManager>();
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        if(netManager != null)
        {
            if(isServer)
            {
                GameObject.FindWithTag("PlayersManager").GetComponent<PlayersManager>().IsolateHost(this);
            }
            else
            {
                GameObject.FindWithTag("PlayersManager").GetComponent<PlayersManager>().IsolateClient(this);
            }
        }
    }

    public void Close()
    {
        Destroy(netManagerObject);
        NetworkManager.networkSceneName = "";
        GameObject playersManager = GameObject.FindWithTag("PlayersManager");
        if (playersManager != null) Destroy(playersManager);
        GameObject playersFinder = GameObject.FindWithTag("PlayersFinder");
        if (playersFinder != null) Destroy(playersFinder);
        GameObject player = GameObject.FindWithTag("LocalPlayer");
        if (player != null) Destroy(player);
        SceneManager.LoadScene(scene);
    }
}
