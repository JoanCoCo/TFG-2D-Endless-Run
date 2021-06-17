using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ForwardHorizontalDisplacement : NetworkBehaviour
{
    private float lastPos = 0.0f;
    private float initialDiff = 0.0f;
    private bool hasBeenInit = false;
    private bool iWasServer;

    private void Start()
    {
        if (isServer)
        {
            Messenger<float>.AddListener(GameEvent.PLAYER_STARTS, OnPlayersStarts);
            Messenger<float>.AddListener(GameEvent.LAST_PLAYER_POSITION_CHANGED, OnLastPlayerPositionChanged);
        }
        iWasServer = isServer;
    }

    private void OnPlayersStarts(float posX)
    {
        if (!hasBeenInit || (hasBeenInit && lastPos > posX))
        {
            lastPos = posX;
            initialDiff = gameObject.transform.position.x - lastPos;
            hasBeenInit = true;
        }
    }

    private void OnLastPlayerPositionChanged(float posX)
    {
        if(lastPos < posX)
        {
            lastPos = posX;
            gameObject.transform.position = new Vector3(initialDiff + lastPos,
                    gameObject.transform.position.y, gameObject.transform.position.z);
        }
    }

    private void OnDestroy()
    {
        if (iWasServer)
        {
            Messenger<float>.RemoveListener(GameEvent.PLAYER_STARTS, OnPlayersStarts);
            Messenger<float>.RemoveListener(GameEvent.LAST_PLAYER_POSITION_CHANGED, OnLastPlayerPositionChanged);
        }
    }
}
