using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public abstract class StreamManager : NetworkBehaviour, IMediaInputManager
{
    protected abstract class StreamMsgType
    {
        public short Header;
        public short Chunk;

        public abstract void UpdateTypes(int n);
    }

    protected class StreamHeaderMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int chunkSize;
        public long timeStamp;

        public StreamHeaderMessage() { }

        public StreamHeaderMessage(uint netId, uint id, int s)
        {
            this.netId = netId;
            this.id = id;
            chunkSize = s;
            timeStamp = System.DateTime.Now.ToUniversalTime().Ticks;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32(id);
            writer.Write(chunkSize);
            writer.Write(timeStamp);
        }

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            id = reader.ReadPackedUInt32();
            chunkSize = reader.ReadInt32();
            timeStamp = reader.ReadInt64();
        }
    }

    protected class StreamChunkMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int order;
        public int size;

        public StreamChunkMessage() { }

        public StreamChunkMessage(uint netId, uint id, int o)
        {
            this.netId = netId;
            this.id = id;
            order = o;
            size = 0;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32(id);
            writer.Write(order);
            writer.Write(size);
        }

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            id = reader.ReadPackedUInt32();
            order = reader.ReadInt32();
            size = reader.ReadInt32();
        }
    }

    protected class StreamMessageDataSupport<Struc, chunkMsg> : MonoBehaviour where chunkMsg : StreamChunkMessage
    {
        public Dictionary<uint, bool> streamWasFullyReceived = new Dictionary<uint, bool>();
        public Dictionary<uint, Struc> streamData = new Dictionary<uint, Struc>();
        public KeySortedList<uint, long> streamIdsReceived = new KeySortedList<uint, long>();
        public List<(chunkMsg, float)> unheadedChunks = new List<(chunkMsg, float)>();
        public Dictionary<uint, int> amountOfEarlyChunks = new Dictionary<uint, int>();

        public void RemoveStream(uint id)
        {
            if (streamIdsReceived.Contains(id)) streamIdsReceived.Remove(id);
            if (streamWasFullyReceived.ContainsKey(id)) streamWasFullyReceived.Remove(id);
            if (streamData.ContainsKey(id)) streamData.Remove(id);
            if (amountOfEarlyChunks.ContainsKey(id)) amountOfEarlyChunks.Remove(id);
        }

        public void RemoveAllStreams()
        {
            for (int i = 0; i < streamIdsReceived.Count; i++)
            {
                RemoveStream(streamIdsReceived[0]);
            }
        }
    }

    protected delegate void RegenerateStreamFromReceivedData(uint id);

    [SerializeField] protected const int maxChunkSize = short.MaxValue;
    [SerializeField] protected float streamTimeout = 1.0f;
    [SerializeField] protected float pendingHeadersTimeout = 5.0f;
    [SerializeField] protected int transmissionsPerSecond = 20;

    protected float elapsedTime = 0.0f;

    protected NetworkIdentity networkIdentity;
    protected uint nextId = 0;

    protected long lastStreamTimestamp = System.DateTime.Now.ToUniversalTime().Ticks;

    protected int channel;

    [SyncVar] protected bool isStreamOn = false;

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

    protected void UpdateStream<Struc, chunkMsg>(StreamMessageDataSupport<Struc, chunkMsg> msgData, RegenerateStreamFromReceivedData Regenerator) where chunkMsg : StreamChunkMessage
    {
        if (isStreamOn)
        {
            try
            {
                if (msgData.streamIdsReceived.Count > 0)
                {
                    if (msgData.streamWasFullyReceived[msgData.streamIdsReceived[0]])
                    {
                        if (msgData.streamIdsReceived.KeyAt(0) > lastStreamTimestamp)
                        {
                            lastStreamTimestamp = msgData.streamIdsReceived.KeyAt(0);
                            Debug.Log("Last timestamp: " + lastStreamTimestamp);
                            Regenerator(msgData.streamIdsReceived[0]);
                        }
                        else
                        {
                            msgData.RemoveStream(msgData.streamIdsReceived[0]);
                        }
                    }
                }
            }
            catch
            {
                msgData.streamIdsReceived.RemoveAt(0);
            }
        } else
        {
            Debug.Log("Stream is not on");
        }
    }

    protected void ManageUnheadedChunks<Struc, chunkMsg>(StreamMessageDataSupport<Struc, chunkMsg> msgData) where chunkMsg : StreamChunkMessage
    {
        if (msgData.unheadedChunks.Count > 0)
        {
            for (int i = 0; i < msgData.unheadedChunks.Count; i++)
            {
                var chunk = msgData.unheadedChunks[i];
                chunk.Item2 += Time.deltaTime;
                if (chunk.Item2 > pendingHeadersTimeout)
                {
                    uint id = chunk.Item1.id;
                    msgData.unheadedChunks.RemoveAt(i);
                    i -= 1;
                    if (msgData.amountOfEarlyChunks.ContainsKey(id)) msgData.amountOfEarlyChunks.Remove(id);
                }
                else
                {
                    msgData.unheadedChunks[i] = chunk;
                }
            }
        }
    }

    protected void SendHeaderMessage(StreamMsgType type, StreamHeaderMessage msg)
    {
        SendStreamMessage(type.Header, msg);
    }

    protected void SendChunkMessage(StreamMsgType type, StreamChunkMessage msg)
    {
        SendStreamMessage(type.Chunk, msg);
    }

    private void SendStreamMessage(short id, MessageBase msg)
    {
        if (isServer)
        {
            SendStreamMessageFromServer(id, msg);
            //NetworkServer.SendToReady(gameObject, TextureMsgType.Header, headerMessage);
        }
        else
        {
            SendStreamMessageFromClient(id, msg);
            //NetworkManager.singleton.client.Send(TextureMsgType.Header, headerMessage);
        }
    }

    private void SendStreamMessageFromServer(short id, MessageBase msg)
    {
        NetworkServer.SendByChannelToReady(gameObject, id, msg, 2);
    }

    private void SendStreamMessageFromClient(short id, MessageBase msg)
    {
        NetworkManager.singleton.client.SendByChannel(id, msg, 2);
    }

    protected void OnStreamHeaderMessageFromClient(StreamMsgType type, StreamHeaderMessage msg)
    {
        if (msg.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received stream header on server.");
            SendStreamMessageFromServer(type.Header, msg);
        }
    }

    protected void OnStreamChunkMessageFromClient(StreamMsgType type, StreamChunkMessage msg)
    {
        if (msg.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received stream chunk on server.");
            SendStreamMessageFromServer(type.Chunk, msg);
        }
    }

    public virtual void StartRecording() { }
    public virtual void StopRecording() { }

    protected virtual void StreamIsOff() { }
}
