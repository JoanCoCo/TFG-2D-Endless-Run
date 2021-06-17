using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private GameObject lobbyStateMessageObject;
    [SerializeField] private TextMeshProUGUI lobbyStateMessageText;
    [SerializeField] private GameObject newPlayerReadyNotification;
    private TextMeshProUGUI newPlayerReadyText;
    private bool waitingMatch = false;

    void Start()
    {
        Messenger.AddListener(LobbyEvent.WAITING_FOR_MATCH, OnWaitingForMatch);
        Messenger<int>.AddListener(LobbyEvent.MATCH_COUNTDOWN_UPDATE, OnMatchCountdownUpdate);
        Messenger<string>.AddListener(LobbyEvent.NEW_PLAYER_READY_FOR_MATCH, OnNewPlayerReadyNotification);
        lobbyStateMessageObject.SetActive(false);
        newPlayerReadyNotification.SetActive(false);
        newPlayerReadyText = newPlayerReadyNotification.GetComponent<TextMeshProUGUI>();
    }

    private void OnWaitingForMatch()
    {
        lobbyStateMessageObject.SetActive(true);
        waitingMatch = true;
    }

    private void OnMatchCountdownUpdate(int seconds)
    {
        if(waitingMatch)
        {
            lobbyStateMessageText.text = "The match will start in " + seconds.ToString() + "s.";
        }
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(LobbyEvent.WAITING_FOR_MATCH, OnWaitingForMatch);
        Messenger<int>.RemoveListener(LobbyEvent.MATCH_COUNTDOWN_UPDATE, OnMatchCountdownUpdate);
        Messenger<string>.RemoveListener(LobbyEvent.NEW_PLAYER_READY_FOR_MATCH, OnNewPlayerReadyNotification);
    }

    private void OnNewPlayerReadyNotification(string player)
    {
        newPlayerReadyText.text = player + " is ready for a match.";
        StartCoroutine(HideNotification());
    }

    private IEnumerator HideNotification()
    {
        newPlayerReadyNotification.SetActive(true);
        yield return new WaitForSeconds(2);
        newPlayerReadyNotification.SetActive(false);
    }
}
