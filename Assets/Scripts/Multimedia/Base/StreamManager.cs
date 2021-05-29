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

    protected class StreamMessageDataSupport<Struc, headerMsg, chunkMsg>
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        private Dictionary<uint, bool> streamWasFullyReceived;
        private Dictionary<uint, Struc> streamData;
        private KeySortedList<uint, long> streamIdsReceived;
        private List<(chunkMsg, float)> unheadedChunks;
        private Dictionary<uint, int> amountOfEarlyChunks;
        private float unheadedChunksTimeout;

        public float UnheadedChunksTimeout
        {
            set
            {
                unheadedChunksTimeout = value;
            }
            get
            {
                return unheadedChunksTimeout;
            }
        }

        public int Count
        {
            get
            {
                return streamIdsReceived.Count;
            }
        }

        public int UnheadedChunks
        {
            get
            {
                return unheadedChunks.Count;
            }
        }

        public Struc this[uint id]
        {
            get
            {
                return streamData[id];
            }
            set
            {
                streamData[id] = value;
            }
        }

        public uint FirstId
        {
            get
            {
                return streamIdsReceived[0];
            }
        }

        public long FistTimestamp
        {
            get
            {
                return streamIdsReceived.KeyAt(0);
            }
        }

        public StreamMessageDataSupport() : this(0.0f) { }

        public StreamMessageDataSupport(float unheadedChunksTimeout)
        {
            streamWasFullyReceived = new Dictionary<uint, bool>();
            streamData = new Dictionary<uint, Struc>();
            streamIdsReceived = new KeySortedList<uint, long>();
            unheadedChunks = new List<(chunkMsg, float)>();
            amountOfEarlyChunks = new Dictionary<uint, int>();
            this.unheadedChunksTimeout = unheadedChunksTimeout;
        }

        public chunkMsg GetUnheadedChunk(int i)
        {
            return unheadedChunks[i].Item1;
        }

        public bool StreamIsComplete(uint id)
        {
            return streamWasFullyReceived[id];
        }

        public void StreamIsFullyReceived(uint id)
        {
            streamWasFullyReceived[id] = true;
        }

        public void RemoveFirstStream()
        {
            uint id = FirstId;
            streamIdsReceived.RemoveAt(0);
            RemoveStream(id);
        }

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

        public void RecoverEarlyChunks(headerMsg header, Struc streamS, StreamChunkHandler<chunkMsg> SaveChunk)
        {
            streamData[header.id] = streamS;
            streamWasFullyReceived[header.id] = false;
            if (amountOfEarlyChunks.ContainsKey(header.id))
            {
                int i = 0;
                int count = 0;
                while (i < amountOfEarlyChunks[header.id] && i < unheadedChunks.Count)
                {
                    var row = unheadedChunks[i].Item1;
                    if (row.id == header.id)
                    {
                        SaveChunk(row);
                        unheadedChunks.RemoveAt(i);
                        count += 1;
                    }
                    i += 1;
                }
                if (amountOfEarlyChunks[header.id] != count)
                {
                    RemoveStream(header.id);
                }
                amountOfEarlyChunks.Remove(header.id);
            }
        }

        public bool CheckTimestamp(headerMsg header)
        {
            if (streamIdsReceived.Count > 0 && streamIdsReceived.KeyAt(0) >= header.timeStamp)
            {
                RemoveStream(header.id);
                return false;
            }
            else
            {
                streamIdsReceived.Add(header.id, header.timeStamp);
                return true;
            }
        }

        public void AddChunk(chunkMsg chunk, StreamChunkHandler<chunkMsg> SaveChunk)
        {
            if (streamData.ContainsKey(chunk.id))
            {
                SaveChunk(chunk);
            }
            else
            {
                unheadedChunks.Add((chunk, 0.0f));
                if (amountOfEarlyChunks.ContainsKey(chunk.id))
                {
                    amountOfEarlyChunks[chunk.id] += 1;
                }
                else
                {
                    amountOfEarlyChunks[chunk.id] = 1;
                }
                Debug.Log(amountOfEarlyChunks[chunk.id] + " chunk(s) with our previous head received.");
            }
        }

        public void ManageUnheadedChunks()
        {
            if (UnheadedChunks > 0)
            {
                for (int i = 0; i < UnheadedChunks; i++)
                {
                    var chunk = unheadedChunks[i];
                    chunk.Item2 += Time.deltaTime;
                    if (chunk.Item2 > unheadedChunksTimeout)
                    {
                        uint id = chunk.Item1.id;
                        unheadedChunks.RemoveAt(i);
                        i -= 1;
                        if (amountOfEarlyChunks.ContainsKey(id)) amountOfEarlyChunks.Remove(id);
                    }
                    else
                    {
                        unheadedChunks[i] = chunk;
                    }
                }
            }
        }

        public bool Exists(uint id)
        {
            return streamIdsReceived.Contains(id);
        }

        public bool ThereIsDataFor(uint id)
        {
            return streamData.ContainsKey(id);
        }

        public IEnumerator WaitTillReceiveAllTheStream(uint id, WaitingCondition Condition, float maxWaitingTime)
        {
            uint waitingId = id;
            float elapsedTime = 0;
            while (ThereIsDataFor(id) && Condition(id))
            {
                yield return new WaitForSecondsRealtime(0.01f);
                elapsedTime += 0.01f;
                if (elapsedTime > maxWaitingTime)
                {
                    RemoveStream(waitingId);
                    yield break;
                }
            }
            if (ThereIsDataFor(waitingId) && elapsedTime <= maxWaitingTime)
            {
                StreamIsFullyReceived(waitingId);
            }
            else
            {
                RemoveStream(waitingId);
            }
        }
    }

    protected delegate void RegenerateStreamFromReceivedData(uint id);
    protected delegate void StreamHeaderHandler<T>(T msg) where T : StreamHeaderMessage;
    protected delegate void StreamChunkHandler<T>(T msg) where T : StreamChunkMessage;
    protected delegate bool WaitingCondition(uint id);

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

    protected void Initialize
        <Struc, headerMsg, chunkMsg>
        (StreamMessageDataSupport<Struc, headerMsg, chunkMsg> msgData, StreamMsgType type)
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        msgData.UnheadedChunksTimeout = pendingHeadersTimeout;
        networkIdentity = GetComponent<NetworkIdentity>();
        type.UpdateTypes((int)networkIdentity.netId.Value);
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

    protected void BroadcastStream()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= 1.0f / transmissionsPerSecond)
        {
            SendSnapshoot();
            elapsedTime = 0.0f;
        }
    }

    protected Coroutine SendSnapshoot() => StartCoroutine(SendStream());

    protected abstract IEnumerator SendStream();

    protected List<(int, T[], int)> DivideArrayInChunks<T>(T[] data, int chunkSize)
    {
        List<(int, T[], int)> result = new List<(int, T[], int)>();

        int order = 0;
        int size = 0;
        T[] chunk = new T[chunkSize];
        for (int i = 0; i <= data.Length; i++)
        {
            if (i - chunkSize * (order + 1) >= 0 || i == data.Length)
            {
                result.Add((order, chunk, size));
                if (i == data.Length) break;
                order += 1;
                chunk = new T[chunkSize];
                size = 0;
            }
            chunk[i - chunkSize * order] = data[i];
            size += 1;
        }
        return result;
    }

    protected void UpdateStream<Struc, headerMsg, chunkMsg>
        (StreamMessageDataSupport<Struc, headerMsg, chunkMsg> msgData,
        RegenerateStreamFromReceivedData Regenerator)
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        if (isStreamOn)
        {
            try
            {
                if (msgData.Count > 0)
                {
                    if (msgData.StreamIsComplete(msgData.FirstId))
                    {
                        if (msgData.FistTimestamp > lastStreamTimestamp)
                        {
                            lastStreamTimestamp = msgData.FistTimestamp;
                            Debug.Log("Last timestamp: " + lastStreamTimestamp);
                            Regenerator(msgData.FirstId);
                        }
                        else
                        {
                            msgData.RemoveStream(msgData.FirstId);
                        }
                    }
                }
            }
            catch
            {
                msgData.RemoveFirstStream();
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

    protected void OnStreamHeaderMessageFromServer<T>(T msg,
        StreamHeaderHandler<T> OnHeaderReceived) where T : StreamHeaderMessage
    {
        if (msg.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received stream header on client.");
            OnHeaderReceived(msg);
        }
    }

    protected void OnStreamChunkMessageFromServer<T>(T msg,
        StreamChunkHandler<T> OnChunkReceived) where T : StreamChunkMessage
    {
        if (msg.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received stream chunk on client.");
            OnChunkReceived(msg);
        }
    }

    public abstract void StartRecording();
    public abstract void StopRecording();

    protected void StreamIsOnServer()
    {
        Debug.Log("Stream is on.");
        isStreamOn = true;
    }

    protected void StreamIsOffServer()
    {
        isStreamOn = false;
        Debug.Log("Stream is off.");
    }
}
