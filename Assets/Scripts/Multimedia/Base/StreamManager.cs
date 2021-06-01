using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.NetworkVariable;
using MLAPI.Serialization;
using System.IO;
using MLAPI.Serialization.Pooled;


/// <summary>
/// Class that abstracts a streaming media manager,
/// </summary>
public abstract class StreamManager : NetworkBehaviour, IMediaInputManager
{
    /// <summary>
    /// Class that abstracts a message type used for streaming.
    /// </summary>
    protected abstract class StreamMsgType
    {
        /// <summary>
        /// A stream message of the type header.
        /// </summary>
        public string Header;

        /// <summary>
        /// A stream message of the type chunk.
        /// </summary>
        public string Chunk;

        /// <summary>
        /// Function that updates the values of the header and chunk. Takes a number
        /// and modifies Header and Chunk so they are unique to this number.
        /// </summary>
        /// <param name="n">Integer used to generete the new Header and Chunk values.</param>
        public abstract void UpdateTypes(ulong n);
    }

    protected abstract class StreamMessage {
        /// <summary>
        /// Network identifier of the player object who sent the message.
        /// </summary>
        public ulong netId;

        /// <summary>
        /// Identifier of the stream.
        /// </summary>
        public uint id;

        public abstract Stream MessageStream { get; }
    }

    /// <summary>
    /// Class that represents the basic stream header message.
    /// </summary>
    protected class StreamHeaderMessage : StreamMessage, INetworkSerializable
    {
        /// <summary>
        /// Maximum size of the following chunks associated with this header.
        /// </summary>
        public int chunkSize;

        /// <summary>
        /// Time mark used to know when the header was generated.
        /// </summary>
        public long timeStamp;

        public override Stream MessageStream
        {
            get
            {
                MemoryStream stream = new MemoryStream();
                using (PooledNetworkWriter writer = PooledNetworkWriter.Get(stream))
                {
                    writer.WriteUInt64(netId);
                    writer.WriteUInt32(id);
                    writer.WriteInt32(chunkSize);
                    writer.WriteInt64(timeStamp);
                }
                return stream;
            }
        }

        /// <summary>
        /// Constructor to instantiate StreamHeaderMessage
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="s">Maximum size of the following chunks associated with this header.</param>
        public StreamHeaderMessage(ulong netId, uint id, int s)
        {
            this.netId = netId;
            this.id = id;
            chunkSize = s;
            timeStamp = System.DateTime.Now.ToUniversalTime().Ticks;
        }

        public StreamHeaderMessage(Stream stream)
        {
            using(PooledNetworkReader reader = PooledNetworkReader.Get(stream))
            {
                netId = reader.ReadUInt64();
                id = reader.ReadUInt32();
                chunkSize = reader.ReadInt32();
                timeStamp = reader.ReadInt64();
            }
        }

        public virtual void NetworkSerialize(NetworkSerializer serializer)
        {
            serializer.Serialize(ref netId);
            serializer.Serialize(ref id);
            serializer.Serialize(ref chunkSize);
            serializer.Serialize(ref timeStamp);
        }
    }

    protected class StreamChunkMessage : StreamMessage, INetworkSerializable
    {
        /// <summary>
        /// Position in the sequence of chunks with the same id. Starts in cero.
        /// </summary>
        public int order;

        /// <summary>
        /// Real size of the chunk's data.
        /// </summary>
        public int size;

        public override Stream MessageStream
        {
            get
            {
                MemoryStream stream = new MemoryStream();
                using (PooledNetworkWriter writer = PooledNetworkWriter.Get(stream))
                {
                    writer.WriteUInt64(netId);
                    writer.WriteUInt32(id);
                    writer.WriteInt32(order);
                    writer.WriteInt32(size);
                }
                return stream;
            }
        }

        /// <summary>
        /// Constructor to instantiate StreamChunkMessage
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="o">Position in the sequence of chunks with the same id. Starts in zero.</param>
        public StreamChunkMessage(ulong netId, uint id, int o)
        {
            this.netId = netId;
            this.id = id;
            order = o;
            size = 0;
        }

        public StreamChunkMessage(Stream stream)
        {
            using (PooledNetworkReader reader = PooledNetworkReader.Get(stream))
            {
                netId = reader.ReadUInt64();
                id = reader.ReadUInt32();
                order = reader.ReadInt32();
                size = reader.ReadInt32();
            }
        }

        public virtual void NetworkSerialize(NetworkSerializer serializer)
        {
            serializer.Serialize(ref netId);
            serializer.Serialize(ref id);
            serializer.Serialize(ref order);
            serializer.Serialize(ref size);
        }
    }

    /// <summary>
    /// Class that gives support to the data structures needed to manage the media
    /// streaming.
    /// </summary>
    /// <typeparam name="struc">Type struct that represents the media that will be streamed.</typeparam>
    /// <typeparam name="headerMsg">Class derived from StreamHeaderMessage that contains all the additional header information needed for the media.</typeparam>
    /// <typeparam name="chunkMsg">Class derived from StreamChunkMessage that contains all the chunk data needed for the media.</typeparam>
    protected class StreamMessageDataSupport<struc, headerMsg, chunkMsg>
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        /// <summary>
        /// Dictionary that stores the state of each stream received (if it is
        /// complete or not).
        /// </summary>
        private Dictionary<uint, bool> streamWasFullyReceived;

        /// <summary>
        /// Dictionary that stores the reconstructed media.
        /// </summary>
        private Dictionary<uint, struc> streamData;

        /// <summary>
        /// List that contains all the stream ids received ordered by their timestamp.
        /// </summary>
        private KeySortedList<uint, long> streamIdsReceived;

        /// <summary>
        /// List that contains old the chunks whose id has not been registered yet.
        /// </summary>
        private List<(chunkMsg, float)> unheadedChunks;

        /// <summary>
        /// Dictonary that contains the number of early chunks of each id.
        /// </summary>
        private Dictionary<uint, int> amountOfEarlyChunks;

        /// <summary>
        /// Maximum time that the unheaded chunks can be stored.
        /// </summary>
        private float unheadedChunksTimeout;

        /// <summary>
        /// Maximum time that the unheaded chunks can be stored.
        /// </summary>
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

        /// <summary>
        /// Number of elements that have been registered.
        /// </summary>
        public int Count
        {
            get
            {
                return streamIdsReceived.Count;
            }
        }

        /// <summary>
        /// Number of unheaded chunks that have been registered.
        /// </summary>
        public int UnheadedChunks
        {
            get
            {
                return unheadedChunks.Count;
            }
        }

        /// <summary>
        /// Data received for a given id.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        /// <returns></returns>
        public struc this[uint id]
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

        /// <summary>
        /// Identifier of the first element in the streamed sequence received.
        /// </summary>
        public uint FirstId
        {
            get
            {
                return streamIdsReceived[0];
            }
        }

        /// <summary>
        /// Timestamp of the first element in the streamed sequence received.
        /// </summary>
        public long FistTimestamp
        {
            get
            {
                return streamIdsReceived.KeyAt(0);
            }
        }

        /// <summary>
        /// Empty delfault constructor for StreamMessageDataSupport.
        /// The timeout for unheaded chunks is set to 0.
        /// </summary>
        public StreamMessageDataSupport() : this(0.0f) { }

        /// <summary>
        /// Constructor that specified the timeout for the unheaded chunks.
        /// </summary>
        /// <param name="unheadedChunksTimeout">Maximum time that the unheaded chunks can be stored.</param>
        public StreamMessageDataSupport(float unheadedChunksTimeout)
        {
            streamWasFullyReceived = new Dictionary<uint, bool>();
            streamData = new Dictionary<uint, struc>();
            streamIdsReceived = new KeySortedList<uint, long>();
            unheadedChunks = new List<(chunkMsg, float)>();
            amountOfEarlyChunks = new Dictionary<uint, int>();
            this.unheadedChunksTimeout = unheadedChunksTimeout;
        }

        /// <summary>
        /// Obtains the unheaded chunk in the i position of the list.
        /// </summary>
        /// <param name="i">Index of the desired unheaded chunk in the list.</param>
        /// <returns></returns>
        public chunkMsg GetUnheadedChunk(int i)
        {
            return unheadedChunks[i].Item1;
        }

        /// <summary>
        /// Obtains if the data associated to a given id has been fully received.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        /// <returns></returns>
        public bool StreamIsComplete(uint id)
        {
            return streamWasFullyReceived[id];
        }

        /// <summary>
        /// Sets a given stream as fully received.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        public void StreamIsFullyReceived(uint id)
        {
            streamWasFullyReceived[id] = true;
        }

        /// <summary>
        /// Removes the first stream in the sequence received.
        /// </summary>
        public void RemoveFirstStream()
        {
            uint id = FirstId;
            streamIdsReceived.RemoveAt(0);
            RemoveStream(id);
        }

        /// <summary>
        /// Removes all the data stored regarding a given id.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        public void RemoveStream(uint id)
        {
            if (streamIdsReceived.Contains(id)) streamIdsReceived.Remove(id);
            if (streamWasFullyReceived.ContainsKey(id)) streamWasFullyReceived.Remove(id);
            if (streamData.ContainsKey(id)) streamData.Remove(id);
            if (amountOfEarlyChunks.ContainsKey(id)) amountOfEarlyChunks.Remove(id);
        }

        /// <summary>
        /// Removes the data stored regarding all the ids received.
        /// </summary>
        public void RemoveAllStreams()
        {
            for (int i = 0; i < streamIdsReceived.Count; i++)
            {
                RemoveStream(streamIdsReceived[0]);
            }
        }

        /// <summary>
        /// Saves the media associated to a given header message and looks for
        /// unheaded chunks of this header to save their data on the media structure.
        /// </summary>
        /// <param name="header">Header message.</param>
        /// <param name="streamS">Media associated to the header message.</param>
        /// <param name="SaveChunk">Methodd that takes a chunk message and saves its data.</param>
        public void RecoverEarlyChunks(headerMsg header, struc streamS, StreamChunkHandler<chunkMsg> SaveChunk)
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

        /// <summary>
        /// Checks if a given header has arrived late. If its timestamp is lower or
        /// equal than the first timestamp in the registered sequence then the information
        /// saved for this header message is discarded. If not, then the header is added.
        /// </summary>
        /// <param name="header">Header message.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Adds the data contain in a chunk message to the appopiate media structure.
        /// </summary>
        /// <param name="chunk">Chunk message.</param>
        /// <param name="SaveChunk">Methodd that takes a chunk message and saves its data.</param>
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

        /// <summary>
        /// Method that updated the life time of the unheded chunks and erases them
        /// when the unheaded chunk timeout expires. This method is meant to be
        /// excuted in each update.
        /// </summary>
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

        /// <summary>
        /// Checks if a given id is currently registered.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        /// <returns></returns>
        public bool Exists(uint id)
        {
            return streamIdsReceived.Contains(id);
        }

        /// <summary>
        /// Checks if there is a media structure being built for a given id.
        /// </summary>
        /// <param name="id">Identifier of a stream.</param>
        /// <returns></returns>
        public bool ThereIsDataFor(uint id)
        {
            return streamData.ContainsKey(id);
        }

        /// <summary>
        /// Corutine that waits until a given condition or until the maximum
        /// waiting time is reached. It is meant to be used for waiting for the
        /// arrival of the chunks associated to a header.
        /// </summary>
        /// <param name="header">Header message.</param>
        /// <param name="Condition">Method that defines the condition for which the corutine will have to keep waiting.</param>
        /// <param name="maxWaitingTime">Maximum time that the corutine can wait.</param>
        /// <returns></returns>
        public IEnumerator WaitTillReceiveAllTheStream(headerMsg header, WaitingCondition Condition, float maxWaitingTime)
        {
            uint waitingId = header.id;
            float elapsedTime = 0;
            while (ThereIsDataFor(waitingId) && Condition(waitingId))
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

    /// <summary>
    /// Profile of a method used to regenerate on object from the received stream. 
    /// </summary>
    /// <param name="id">Identifier of a stream.</param>
    protected delegate void RegenerateStreamFromReceivedData(uint id);

    /// <summary>
    /// Profile of a method used to process header messages.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="msg">Header message.</param>
    protected delegate void StreamHeaderHandler<T>(T msg) where T : StreamHeaderMessage;

    /// <summary>
    /// Profile of a method used to process chunk messages.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="msg">Chunk message.</param>
    protected delegate void StreamChunkHandler<T>(T msg) where T : StreamChunkMessage;

    /// <summary>
    /// Profile of a method used to define a waiting condition.
    /// </summary>
    /// <param name="id">Identifier of a stream.</param>
    /// <returns></returns>
    protected delegate bool WaitingCondition(uint id);

    /// <summary>
    /// Maximum size allowed for the chunks.
    /// </summary>
    [SerializeField] protected const int maxChunkSize = short.MaxValue;

    /// <summary>
    /// Maximum time an incomplete stream can be hold.
    /// </summary>
    [SerializeField] protected float streamTimeout = 1.0f;

    /// <summary>
    /// Maimum time an unheaded chunk can be hold.
    /// </summary>
    [SerializeField] protected float pendingHeadersTimeout = 5.0f;

    /// <summary>
    /// Number of transmition to be performed in one second.
    /// </summary>
    [SerializeField] protected int transmissionsPerSecond = 20;

    /// <summary>
    /// Elapsed time since the last transmission.
    /// </summary>
    protected float elapsedTime = 0.0f;

    /// <summary>
    /// Network identity of this object.
    /// </summary>
    protected NetworkObject networkIdentity;
    protected uint nextId = 0;

    /// <summary>
    /// Timestap of the last stream that was regenerated.
    /// </summary>
    protected long lastStreamTimestamp = System.DateTime.Now.ToUniversalTime().Ticks;

    /// <summary>
    /// Channel to be used for the transmission.
    /// </summary>
    protected int channel;

    /// <summary>
    /// Bool used to synchronize if the streaming is tuned on or off.
    /// </summary>
    protected NetworkVariable<bool> isStreamOn = new NetworkVariable<bool>();

    private const string SERVER_MSGTYPE = "Server";
    private const string CLIENT_MSGTYPE = "Client";

    /// <summary>
    /// Initializes the manager, setting up the network identity, updating the
    /// message type identifiers and setting up the unheaded chunks timeout.
    /// </summary>
    /// <typeparam name="Struc">Type struct that represents the media that will be streamed.</typeparam>
    /// <typeparam name="headerMsg">Class derived from StreamHeaderMessage that contains all the additional header information needed for the media.</typeparam>
    /// <typeparam name="chunkMsg">Class dderived from StreamChunkMessage that contains all the chunk data needed for the media.</typeparam>
    /// <param name="msgData">Data structure containing all the information received.</param>
    /// <param name="type">Message type that is being used.</param>
    protected void Initialize
        <Struc, headerMsg, chunkMsg>
        (StreamMessageDataSupport<Struc, headerMsg, chunkMsg> msgData, StreamMsgType type)
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        isStreamOn.Value = false;
        msgData.UnheadedChunksTimeout = pendingHeadersTimeout;
        networkIdentity = GetComponent<NetworkObject>();
        type.UpdateTypes(networkIdentity.NetworkObjectId);
    }

    /// <summary>
    /// Creates the handlers needed to receive messages.
    /// </summary>
    /// <param name="msgType">Message type that is being used.</param>
    /// <param name="headerMessageFromClient">Method to handle the header messages on the server.</param>
    /// <param name="chunkMessageFromClient">Method to handle the chunk messages on the server.</param>
    /// <param name="headerMessageFromServer">Method to handle the header messages on the client.</param>
    /// <param name="chunkMessageFromServer">Method to handle the chunk messages on the client.</param>
    protected void CreateHandlers(StreamMsgType msgType,
        CustomMessagingManager.HandleNamedMessageDelegate headerMessageFromClient,
        CustomMessagingManager.HandleNamedMessageDelegate chunkMessageFromClient,
        CustomMessagingManager.HandleNamedMessageDelegate headerMessageFromServer,
        CustomMessagingManager.HandleNamedMessageDelegate chunkMessageFromServer)
    {
        if (IsServer)
        {
            Debug.Log("Registering server handlers.");
            CustomMessagingManager.RegisterNamedMessageHandler(msgType.Header + SERVER_MSGTYPE, headerMessageFromClient);
            //NetworkServer.RegisterHandler(msgType.Header, headerMessageFromClient);
            CustomMessagingManager.RegisterNamedMessageHandler(msgType.Chunk + SERVER_MSGTYPE, chunkMessageFromClient);
            //NetworkServer.RegisterHandler(msgType.Chunk, chunkMessagerFromClient);
        }
        else if (IsClient)
        {
            Debug.Log("Registering client handlers.");
            CustomMessagingManager.RegisterNamedMessageHandler(msgType.Header + CLIENT_MSGTYPE, headerMessageFromServer);
            //NetworkManager.singleton.client.RegisterHandler(msgType.Header, headerMessageFromServer);
            CustomMessagingManager.RegisterNamedMessageHandler(msgType.Chunk + CLIENT_MSGTYPE, chunkMessageFromServer);
            //NetworkManager.singleton.client.RegisterHandler(msgType.Chunk, chunkMessageFromServer);
        }
    }

    /// <summary>
    /// Destroys the handlers created to receive messages.
    /// </summary>
    /// <param name="msgType">Message type that is being used.</param>
    protected void DestroyHandlers(StreamMsgType msgType)
    {
        if (IsServer)
        {
            CustomMessagingManager.UnregisterNamedMessageHandler(msgType.Header + SERVER_MSGTYPE);
            CustomMessagingManager.UnregisterNamedMessageHandler(msgType.Chunk + SERVER_MSGTYPE);
        }
        else if (IsClient && NetworkManager.Singleton != null)
        {
            CustomMessagingManager.UnregisterNamedMessageHandler(msgType.Header + CLIENT_MSGTYPE);
            CustomMessagingManager.UnregisterNamedMessageHandler(msgType.Chunk + CLIENT_MSGTYPE);
        }
    }

    /// <summary>
    /// Sends a stream periodically given the number of transmissions per second.
    /// It is intended to be executed once in every update.
    /// </summary>
    protected void BroadcastStream()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= 1.0f / transmissionsPerSecond)
        {
            SendSnapshoot();
            elapsedTime = 0.0f;
        }
    }

    /// <summary>
    /// Triggers the corutine responsible to convert the media to be sent in header
    /// and chunk messages.
    /// </summary>
    /// <returns>Corutine that converts the media to be sent in header and chunk messages.</returns>
    protected Coroutine SendSnapshoot() => StartCoroutine(SendStream());

    /// <summary>
    /// Corutine that converts the media to be sent in header and chunk messages.
    /// </summary>
    /// <returns></returns>
    protected abstract IEnumerator SendStream();

    /// <summary>
    /// Divides a given array into chunks to be sent.
    /// </summary>
    /// <typeparam name="T">Type of the array to be divided.</typeparam>
    /// <param name="data">Array to be divided.</param>
    /// <param name="chunkSize">Maximum size of the chunks.</param>
    /// <returns>Triplet of the form (order, data, size).</returns>
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

    /// <summary>
    /// Regenerates the streams received in order when they are complete.
    /// </summary>
    /// <typeparam name="Struc">Type struct that represents the media that will be streamed.</typeparam>
    /// <typeparam name="headerMsg">Class derived from StreamHeaderMessage that contains all the additional header information needed for the media.</typeparam>
    /// <typeparam name="chunkMsg">Class dderived from StreamChunkMessage that contains all the chunk data needed for the media.</typeparam>
    /// <param name="msgData">Data structure containing all the information received.</param>
    /// <param name="Regenerator">Method that regenerates the object from the data associated to an id.</param>
    protected void UpdateStream<Struc, headerMsg, chunkMsg>
        (StreamMessageDataSupport<Struc, headerMsg, chunkMsg> msgData,
        RegenerateStreamFromReceivedData Regenerator)
        where chunkMsg : StreamChunkMessage
        where headerMsg : StreamHeaderMessage
    {
        if (isStreamOn.Value)
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

    /// <summary>
    /// Sends a header message.
    /// </summary>
    /// <param name="type">Message type that is being used.</param>
    /// <param name="msg">Header message to be sent.</param>
    protected void SendHeaderMessage(StreamMsgType type, StreamHeaderMessage msg)
    {
        SendStreamMessage(type.Header, msg);
    }

    /// <summary>
    /// Sends a chunk message.
    /// </summary>
    /// <param name="type">Message type that is being used.</param>
    /// <param name="msg">Chunk message to be sent.</param>
    protected void SendChunkMessage(StreamMsgType type, StreamChunkMessage msg)
    {
        SendStreamMessage(type.Chunk, msg);
    }

    /// <summary>
    /// Sends a message.
    /// </summary>
    /// <param name="id">Type of message that is being sent.</param>
    /// <param name="msg">Message to be sent.</param>
    private void SendStreamMessage(string id, StreamMessage msg)
    {
        if (IsServer)
        {
            SendStreamMessageFromServer(id, msg);
        }
        else
        {
            SendStreamMessageFromClient(id, msg);
        }
    }

    /// <summary>
    /// Sends a message from the server to all the clients.
    /// </summary>
    /// <param name="id">Type of message that is being sent.</param>
    /// <param name="msg">Message to be sent.</param>
    private void SendStreamMessageFromServer(string id, StreamMessage msg)
    {
        //NetworkServer.SendByChannelToReady(gameObject, id, msg, 2);
        Debug.Log("Sending to client stream of length: " + msg.MessageStream.Length);
        foreach (var client in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            if (client != NetworkManager.LocalClientId)
            {
                CustomMessagingManager.SendNamedMessage(id + CLIENT_MSGTYPE, client, msg.MessageStream);
            }
        }
    }

    /// <summary>
    /// Sends a message from the client to the sever.
    /// </summary>
    /// <param name="id">Type of message that is being sent.</param>
    /// <param name="msg">Message to be sent.</param>
    private void SendStreamMessageFromClient(string id, StreamMessage msg)
    {
        //NetworkManager.singleton.client.SendByChannel(id, msg, 2);
        Debug.Log("Sending to server stream of length: " + msg.MessageStream.Length);
        CustomMessagingManager.SendNamedMessage(id + SERVER_MSGTYPE, NetworkManager.Singleton.ServerClientId, msg.MessageStream);
    }

    /// <summary>
    /// Manages the arrival of a header message on the server broadcasting it to
    /// all clients.
    /// </summary>
    /// <param name="type">Message type that is being used.</param>
    /// <param name="msg"></param>
    protected void OnStreamHeaderMessageFromClient(StreamMsgType type, StreamHeaderMessage msg)
    {
        if (msg.netId == networkIdentity.NetworkObjectId)
        {
            Debug.Log("Received stream header on server.");
            SendStreamMessageFromServer(type.Header, msg);
        }
    }

    /// <summary>
    /// Manages the arrival of a chunk message on the server, broadcasting
    /// </summary>
    /// <param name="type">Message type that is being used.</param>
    /// <param name="msg"></param>
    protected void OnStreamChunkMessageFromClient(StreamMsgType type, StreamChunkMessage msg)
    {
        if (msg.netId == networkIdentity.NetworkObjectId)
        {
            Debug.Log("Received stream chunk on server.");
            SendStreamMessageFromServer(type.Chunk, msg);
        }
    }

    /// <summary>
    /// Manages the arrival of a header message on the client, calling a given
    /// function to process it.
    /// </summary>
    /// <typeparam name="T">Class derived from StreamHeaderMessage that contains all the additional header information needed for the media.</typeparam>
    /// <param name="msg">Header message.</param>
    /// <param name="OnHeaderReceived">Method that processes the header message and saves its data.</param>
    protected void OnStreamHeaderMessageFromServer<T>(T msg,
        StreamHeaderHandler<T> OnHeaderReceived) where T : StreamHeaderMessage
    {
        if (msg.netId == networkIdentity.NetworkObjectId)
        {
            Debug.Log("Received stream header on client.");
            OnHeaderReceived(msg);
        }
    }

    /// <summary>
    /// Manages the arrival of a chunk message on the client, calling a given
    /// function to process it.
    /// </summary>
    /// <typeparam name="T">Class derived from StreamChunkMessage that contains all the chunk data needed for the media.</typeparam>
    /// <param name="msg">Chunk message.</param>
    /// <param name="OnChunkReceived">Method that processes the chunk message and saves its data.</param>
    protected void OnStreamChunkMessageFromServer<T>(T msg,
        StreamChunkHandler<T> OnChunkReceived) where T : StreamChunkMessage
    {
        if (msg.netId == networkIdentity.NetworkObjectId)
        {
            Debug.Log("Received stream chunk on client.");
            OnChunkReceived(msg);
        }
    }

    public abstract void StartRecording();
    public abstract void StopRecording();

    /// <summary>
    /// Activates the streaming.
    /// </summary>
    protected void StreamIsOnServer()
    {
        Debug.Log("Stream is on.");
        isStreamOn.Value = true;
    }

    /// <summary>
    /// Stops the streaming.
    /// </summary>
    protected void StreamIsOffServer()
    {
        isStreamOn.Value = false;
        Debug.Log("Stream is off.");
    }
}
