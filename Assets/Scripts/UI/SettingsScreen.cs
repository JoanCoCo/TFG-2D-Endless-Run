using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] private KeyCode m_exitKey = KeyCode.Escape;
    [SerializeField] private GameObject m_settingsScreen;
    [SerializeField] private TMP_Dropdown camDropdown;
    [SerializeField] private TMP_Dropdown micDropdown;

    private void Start()
    {
        m_settingsScreen.SetActive(false);
        List<string> options = new List<string>();

        string targetCam = PlayerPrefs.GetString("Cam");
        string targetMic = PlayerPrefs.GetString("Mic");
        int i = 0;
        foreach (var mic in Microphone.devices)
        {
            options.Add(mic);
            if (mic.Equals(targetMic)) i = options.Count - 1;
        }
        micDropdown.AddOptions(options);
        micDropdown.value = i;
        options.Clear();
        i = 0;
        foreach(var cam in WebCamTexture.devices)
        {
            options.Add(cam.name);
            if (cam.name.Equals(targetCam)) i = options.Count - 1;
        }
        camDropdown.AddOptions(options);
        camDropdown.value = i;

        micDropdown.onValueChanged.AddListener(OnMicSelected);
        camDropdown.onValueChanged.AddListener(OnCamSelected);
    }

    public void ToggleSettings()
    {
        m_settingsScreen.SetActive(!m_settingsScreen.activeSelf);
    }

    public void OnCamSelected(int index)
    {
        PlayerPrefs.SetString("Cam", camDropdown.options[index].text);
    }

    public void OnMicSelected(int index)
    {
        PlayerPrefs.SetString("Mic", micDropdown.options[index].text);
    }
}
