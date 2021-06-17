using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MatchStarter : DistributedEntityBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private NetworkManager netManager;
    [SerializeField] private int minNumberOfPlayers;
    [SerializeField] private int maxNumberOfPlayers;

    [SyncVar]
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
        if (readyConfirmationPending && currNumberOfPlayers < maxNumberOfPlayers)
        {
            readyConfirmationPending = false;
            Messenger.Broadcast(LobbyEvent.WAITING_FOR_MATCH);
            RunCommand(UpdateNumberOfPlayersCommandCapsule, PlayerPrefs.GetString("Name"));
        }
    }

    private void UpdateNumberOfPlayersCommandCapsule(string player)
    {
        CmdUpdateNumberOfReadyPlayers(player);
    }

    [Command]
    private void CmdUpdateNumberOfReadyPlayers(string player)
    {
        if (currNumberOfPlayers < maxNumberOfPlayers)
        {
            currNumberOfPlayers += 1;
            if (gameIsStarting) RpcGetReadyForMatch();
            Debug.Log("Server Ready Players: " + currNumberOfPlayers.ToString());
        }
        RpcNewPlayerReadyForMatch(player);
        RemoveOwnership();
    }

    [ClientRpc]
    private void RpcGetReadyForMatch()
    {
        if (!readyConfirmationPending)
        {
            gameIsStarting = true;
        }
    }

    [ClientRpc]
    private void RpcNewPlayerReadyForMatch(string player)
    {
        Messenger<string>.Broadcast(LobbyEvent.NEW_PLAYER_READY_FOR_MATCH, player);
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
