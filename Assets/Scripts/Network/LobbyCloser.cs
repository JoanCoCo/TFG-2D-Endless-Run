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
                netManager.StopHost();
            } else
            {
                netManager.StopClient();
            }
            Destroy(netManagerObject);
            SceneManager.LoadScene(scene);
        }
    }
}
