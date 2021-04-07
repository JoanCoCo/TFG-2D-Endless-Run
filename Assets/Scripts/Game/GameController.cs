using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private float elapsedTime = 0.0f;
    private bool gameIsPaused = false;
    private bool playerDied = false;
    private bool gameFinished = false;

    // Start is called before the first frame update
    void Start()
    {
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
    }

    // Update is called once per frame
    void Update()
    {
        if (!playerDied)
        {
            elapsedTime += Time.deltaTime;
            Messenger<int>.Broadcast(GameEvent.TIME_PASSED, (int)elapsedTime);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                gameIsPaused = !gameIsPaused;
                if (gameIsPaused)
                {
                    Time.timeScale = 0;
                    Messenger.Broadcast(GameEvent.PAUSE);
                }
                else
                {
                    Time.timeScale = 1;
                    Messenger.Broadcast(GameEvent.RESUME);
                }
            }
        } else if(!gameFinished)
        {
            Messenger.Broadcast(GameEvent.PLAYER_DIED);
            float pastScore = PlayerPrefs.GetFloat("HighScore", 0.0f);
            if(elapsedTime > pastScore)
            {
                PlayerPrefs.SetFloat("HighScore", elapsedTime);
                Messenger.Broadcast(GameEvent.NEW_HIGHSCORE_REACHED);
            } else
            {
                PlayerPrefs.SetFloat("LastScore", elapsedTime);
            }
            gameFinished = true;
            Time.timeScale = 0;
        }
    }

    private void OnPlayerDeath()
    {
        playerDied = true;
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
    }
}
