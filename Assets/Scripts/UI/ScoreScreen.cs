using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    private bool isNewHighscore = false;
    // Start is called before the first frame update
    void Start()
    {
        Messenger.AddListener(GameEvent.NEW_HIGHSCORE_REACHED, OnNewHighscore);
    }

    // Update is called once per frame
    void Update()
    {
        if(isNewHighscore)
        {
            scoreText.text = "New highscore: " + GetScoreString();
        } else
        {
            scoreText.text = "Score: " + GetScoreString();
        }
    }

    private void OnNewHighscore()
    {
        isNewHighscore = true;
    }

    private string GetScoreString()
    {
        int d = (int) PlayerPrefs.GetFloat( (isNewHighscore) ? "HighScore" : "LastScore", 0.0f);
        //string s = (time % 60) >= 10 ? (time % 60).ToString() : "0" + (time % 60);
        return d.ToString() + " m";
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(GameEvent.NEW_HIGHSCORE_REACHED, OnNewHighscore);
    }
}
