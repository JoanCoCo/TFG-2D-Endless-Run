using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MediaInputController : MonoBehaviour
{
    [SerializeField] private StreamManager mediaInputManager;
    [SerializeField] private GameObject onImage;
    [SerializeField] private GameObject offImage;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private KeyCode interactionKey;
    private enum State { On, Off };
    [SerializeField] private State state = State.Off;
    private InputAvailabilityManager inputAvailabilityManager;

    // Start is called before the first frame update
    void Start()
    {
        keyText.text = interactionKey.ToString();
        inputAvailabilityManager = GameObject.FindWithTag("InputAvailabilityManager").GetComponent<InputAvailabilityManager>();
        UpdateState();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(interactionKey)
            && (inputAvailabilityManager == null
            || !inputAvailabilityManager.UserIsTyping)) { ChangeState(); }
    }

    private void UpdateState()
    {
        offImage.SetActive(state == State.Off);
        onImage.SetActive(state == State.On);
        if(state == State.On)
        {
            if(mediaInputManager != null) mediaInputManager.StartRecording();
        } else
        {
            if(mediaInputManager != null) mediaInputManager.StopRecording();
        }
    }

    private void ChangeState()
    {
        state = (state == State.On) ? State.Off : State.On;
        UpdateState();
    }

    public void SetMedia(StreamManager mediaInputManager)
    {
        this.mediaInputManager = mediaInputManager;
        UpdateState();
    }

    public bool IsNotSet() { return mediaInputManager == null; }
}
