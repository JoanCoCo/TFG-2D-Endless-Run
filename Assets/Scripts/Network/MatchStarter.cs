using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class MatchStarter : DistributedEntitieBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private NetworkManager netManager;
    [SerializeField] private int minNumberOfPlayers;
    [SerializeField] private int maxNumberOfPlayers;

    int currNumberOfPlayers = 0;

    private bool readyConfirmationPending = true;
    private bool gameIsStarting = false;
    [SerializeField] private float matchCountdown = 60;
    [SerializeField] private string gameScene;
    private NetworkIdentity netIdentity;
    private float currentCountdown = 0; 

    private void Start()
    {
        if (netManager == null) netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        netIdentity = GetComponent<NetworkIdentity>();
        currentCountdown = matchCountdown;
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        if (readyConfirmationPending)
        {
            readyConfirmationPending = false;
            Messenger.Broadcast(LobbyEvent.WAITING_FOR_MATCH);
            RunCommand(UpdateNumberOfPlayersCommandCapsule);
        }
    }

    private void UpdateNumberOfPlayersCommandCapsule()
    {
        CmdUpdateNumberOfReadyPlayers();
    }

    [Command]
    private void CmdUpdateNumberOfReadyPlayers()
    {
        if (currNumberOfPlayers < maxNumberOfPlayers)
        {
            currNumberOfPlayers += 1;
            RpcUpdateNumberOfReadyPlayers(currNumberOfPlayers);
            if (gameIsStarting) RpcGetReadyForMatch();
            Debug.Log("Server Ready Players: " + currNumberOfPlayers.ToString());
        }
        RemoveOwnership();
    }

    [ClientRpc]
    private void RpcUpdateNumberOfReadyPlayers(int n)
    {
        currNumberOfPlayers = n;
        Debug.Log("Ready players: " + currNumberOfPlayers);
    }

    [ClientRpc]
    private void RpcGetReadyForMatch()
    {
        gameIsStarting = true;
    }

    [ClientRpc]
    private void RpcUpdateCountdown(float countdown)
    {
        if(gameIsStarting)
        {
            currentCountdown = countdown;
            Messenger<int>.Broadcast(LobbyEvent.MATCH_COUNTDOWN_UPDATE, (int) countdown);
        }
    }

    private void Update()
    {
        if(isServer)
        {
            if(currNumberOfPlayers >= minNumberOfPlayers && !gameIsStarting)
            {
                gameIsStarting = true;
                RpcGetReadyForMatch();
            }

            if (gameIsStarting)
            {
                if (currentCountdown > 0.0f)
                {
                    RpcUpdateCountdown(currentCountdown - Time.deltaTime);
                }
                else
                {
                    //netManager.ServerChangeScene(gameScene);
                    //Messenger.Broadcast(NetworkEvent.SPLIT);
                    RpcSendSplit();
                    currNumberOfPlayers = 0;
                    readyConfirmationPending = true;
                    gameIsStarting = false;
                    currentCountdown = matchCountdown;
                    RpcUpdateCountdown(currentCountdown);
                }
            }
        }
    }

    [ClientRpc]
    private void RpcSendSplit()
    {
        RemoveOwnership();
        Messenger<string>.Broadcast(NetworkEvent.SPLIT, gameScene);
    }
}
