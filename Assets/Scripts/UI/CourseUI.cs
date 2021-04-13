using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CourseUI : MonoBehaviour
{
    [SerializeField] private GameObject healthBar;
    private RectTransform healthBarInitialTransform;
    [SerializeField] private TextMeshProUGUI chronoText;
    [SerializeField] private TextMeshProUGUI distaceText;
    [SerializeField] private GameObject pausedScreen;
    [SerializeField] private GameObject finishedScreen;

    // Start is called before the first frame update
    void Start()
    {
        healthBarInitialTransform = healthBar.GetComponent<RectTransform>();
        Messenger<float>.AddListener(GameEvent.PLAYER_HEALTH_CHANGED, OnHealthChange);
        Messenger<int>.AddListener(GameEvent.TIME_PASSED, OnTimePassed);
        Messenger.AddListener(GameEvent.PAUSE, OnChangeState);
        Messenger.AddListener(GameEvent.RESUME, OnChangeState);
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger<int>.AddListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
        pausedScreen.SetActive(false);
        finishedScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(finishedScreen.activeSelf && Input.GetKey(KeyCode.Return))
        {
            Time.timeScale = 1;
            SceneManager.LoadScene("LobbyScene");
        }
    }

    private void OnHealthChange(float health)
    {
        healthBar.transform.localScale = new Vector3(health, 1.0f, 1.0f);
    }

    private void OnTimePassed(int seconds)
    {
        string s = ((seconds % 60) >= 10) ? (seconds % 60).ToString() : "0" + (seconds % 60);
        chronoText.text = (seconds / 60) + ":" + s;
    }

    private void OnChangeState()
    {
        pausedScreen.SetActive(!pausedScreen.activeSelf);
    }

    private void OnPlayerDeath()
    {
        finishedScreen.SetActive(true);
    }

    private void OnDestroy()
    {
        Messenger<float>.RemoveListener(GameEvent.PLAYER_HEALTH_CHANGED, OnHealthChange);
        Messenger<int>.RemoveListener(GameEvent.TIME_PASSED, OnTimePassed);
        Messenger.RemoveListener(GameEvent.PAUSE, OnChangeState);
        Messenger.RemoveListener(GameEvent.RESUME, OnChangeState);
        Messenger.RemoveListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger<int>.RemoveListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
    }

    private void OnDistanceIncreased(int d)
    {
        distaceText.text = d.ToString() + " m";
    }
}
