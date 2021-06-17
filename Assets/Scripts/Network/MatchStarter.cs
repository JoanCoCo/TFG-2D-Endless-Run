using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.NetworkVariable;
using MLAPI.Messaging;

public class MatchStarter : NetworkBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private int minNumberOfPlayers;
    [SerializeField] private int maxNumberOfPlayers;

    private NetworkVariable<int> currNumberOfPlayers = new NetworkVariable<int>();

    private bool readyConfirmationPending = true;
    private bool gameIsStarting = false;
    [SerializeField] private float matchCountdown = 60;
    [SerializeField] private string gameScene;
    private float currentCountdown = 0; 

    public override void NetworkStart()
    {
        if(IsServer)
        {
            currNumberOfPlayers.Value = 0;
        }
        currentCountdown = matchCountdown;
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        if (readyConfirmationPending && currNumberOfPlayers.Value < maxNumberOfPlayers)
        {
            readyConfirmationPending = false;
            Messenger.Broadcast(LobbyEvent.WAITING_FOR_MATCH);
            UpdateNumberOfReadyPlayersServerRpc(PlayerPrefs.GetString("Name"));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateNumberOfReadyPlayersServerRpc(string player)
    {
        if (currNumberOfPlayers.Value < maxNumberOfPlayers)
        {
            currNumberOfPlayers.Value += 1;
            if (gameIsStarting) GetReadyForMatchClientRpc();
            Debug.Log("Server Ready Players: " + currNumberOfPlayers.Value.ToString());
        }
        NewPlayerReadyForMatchClientRpc(player);
    }

    [ClientRpc]
    private void GetReadyForMatchClientRpc()
    {
        if (!readyConfirmationPending)
        {
            gameIsStarting = true;
        }
    }

    [ClientRpc]
    private void NewPlayerReadyForMatchClientRpc(string player)
    {
        Messenger<string>.Broadcast(LobbyEvent.NEW_PLAYER_READY_FOR_MATCH, player);
    }

    [ClientRpc]
    private void UpdateCountdownClientRpc(float countdown)
    {
        if(gameIsStarting && currentCountdown > countdown)
        {
            currentCountdown = countdown;
            Messenger<int>.Broadcast(LobbyEvent.MATCH_COUNTDOWN_UPDATE, (int) countdown);
        }
    }

    private void Update()
    {
        if(IsServer)
        {
            if(currNumberOfPlayers.Value >= minNumberOfPlayers && !gameIsStarting)
            {
                gameIsStarting = true;
                GetReadyForMatchClientRpc();
            }

            if (gameIsStarting)
            {
                if (currentCountdown > 0.0f)
                {
                    UpdateCountdownClientRpc(currentCountdown - Time.deltaTime);
                }
                else
                {
                    currNumberOfPlayers.Value = 0;
                    readyConfirmationPending = true;
                    gameIsStarting = false;
                    currentCountdown = matchCountdown;
                    Messenger<string>.Broadcast(NetworkEvent.SPLIT, gameScene);
                }
            }
        }
    }
}
