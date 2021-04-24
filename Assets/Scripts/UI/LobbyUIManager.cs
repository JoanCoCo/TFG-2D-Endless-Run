using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private GameObject lobbyStateMessageObject;
    [SerializeField] private TextMeshProUGUI lobbyStateMessageText;
    private bool waitingMatch = false;

    // Start is called before the first frame update
    void Start()
    {
        Messenger.AddListener(LobbyEvent.WAITING_FOR_MATCH, OnWaitingForMatch);
        Messenger<int>.AddListener(LobbyEvent.MATCH_COUNTDOWN_UPDATE, OnMatchCountdownUpdate);
        lobbyStateMessageObject.SetActive(false);
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
    }
}
