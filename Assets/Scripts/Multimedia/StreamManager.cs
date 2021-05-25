using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class StreamManager : NetworkBehaviour
{
    protected abstract class StreamMsgType
    {
        public short Header;
        public short Chunk;

        public abstract void UpdateTypes(int n);
    }

    protected void CreateHandlers(StreamMsgType msgType,
        NetworkMessageDelegate headerMessageFromClient,
        NetworkMessageDelegate chunkMessagerFromClient,
        NetworkMessageDelegate headerMessageFromServer,
        NetworkMessageDelegate chunkMessageFromServer)
    {
        if (isServer)
        {
            Debug.Log("Registering server handlers.");
            if (!NetworkServer.handlers.ContainsKey(msgType.Header))
            {
                NetworkServer.RegisterHandler(msgType.Header, headerMessageFromClient);
            }
            if (!NetworkServer.handlers.ContainsKey(msgType.Chunk))
            {
                NetworkServer.RegisterHandler(msgType.Chunk, chunkMessagerFromClient);
            }
        }
        if (isClient)
        {
            Debug.Log("Registering client handlers.");
            if (!NetworkManager.singleton.client.handlers.ContainsKey(msgType.Header))
            {
                NetworkManager.singleton.client.RegisterHandler(msgType.Header, headerMessageFromServer);
            }
            if (!NetworkManager.singleton.client.handlers.ContainsKey(msgType.Chunk))
            {
                NetworkManager.singleton.client.RegisterHandler(msgType.Chunk, chunkMessageFromServer);
            }
        }
    }

    protected void DestroyHandlers(StreamMsgType msgType)
    {
        if (isServer)
        {
            if (NetworkServer.handlers.ContainsKey(msgType.Header))
            {
                NetworkServer.UnregisterHandler(msgType.Header);
            }
            if (NetworkServer.handlers.ContainsKey(msgType.Chunk))
            {
                NetworkServer.UnregisterHandler(msgType.Chunk);
            }
        }
        if (isClient && NetworkManager.singleton != null && NetworkManager.singleton.client != null)
        {
            if (NetworkManager.singleton.client.handlers.ContainsKey(msgType.Header))
            {
                NetworkManager.singleton.client.UnregisterHandler(msgType.Header);
            }
            if (NetworkManager.singleton.client.handlers.ContainsKey(msgType.Chunk))
            {
                NetworkManager.singleton.client.UnregisterHandler(msgType.Chunk);
            }
        }
    }
}
