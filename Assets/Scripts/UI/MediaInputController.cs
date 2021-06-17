using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MediaInputController : MonoBehaviour
{
    [SerializeField] private StreamManager mediaInputManager;
    [SerializeField] private Sprite onImage;
    [SerializeField] private Sprite offImage;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private Button button;
    [SerializeField] private KeyCode interactionKey;
    private enum State { On, Off };
    [SerializeField] private State state = State.Off;
    private InputAvailabilityManager inputAvailabilityManager;

    void Start()
    {
        keyText.text = interactionKey.ToString();
        inputAvailabilityManager = GameObject.FindWithTag("InputAvailabilityManager").GetComponent<InputAvailabilityManager>();
        button.onClick.AddListener(ChangeState);
        UpdateState();
    }

    void Update()
    {
        if (Input.GetKeyDown(interactionKey)
            && (inputAvailabilityManager == null
            || !inputAvailabilityManager.UserIsTyping)) { ChangeState(); }
    }

    private void UpdateState()
    {
        if(state == State.On)
        {
            button.GetComponent<Image>().sprite = onImage;
            if(mediaInputManager != null) mediaInputManager.StartRecording();
        } else
        {
            button.GetComponent<Image>().sprite = offImage;
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
