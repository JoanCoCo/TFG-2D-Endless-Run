using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerNameManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private string namePrefKey = "Name";

    // Start is called before the first frame update
    void Start()
    {
        nameField.text = PlayerPrefs.GetString(namePrefKey, "Player" + Random.Range(0, 2000));

    }

    public void SaveName()
    {
        if(nameField.text != PlayerPrefs.GetString(namePrefKey))
        {
            PlayerPrefs.SetString(namePrefKey, nameField.text);
            Debug.Log("Name set to " + nameField.text);
        }
    }
}
