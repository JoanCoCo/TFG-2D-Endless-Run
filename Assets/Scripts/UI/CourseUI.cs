using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CourseUI : MonoBehaviour
{
    [SerializeField] private Image healthBar;
    private RectTransform healthBarInitialTransform;

    // Start is called before the first frame update
    void Start()
    {
        healthBarInitialTransform = healthBar.GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        Messenger<int>.AddListener(GameEvent.PLAYER_HEALTH_CHANGED, OnHealthChange);
    }

    private void OnHealthChange(int health)
    {

    }
}
