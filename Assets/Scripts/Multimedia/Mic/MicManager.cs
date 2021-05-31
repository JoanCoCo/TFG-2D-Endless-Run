using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using System.IO;
using MLAPI.Messaging;

/// <summary>
/// Class that implements a microphone streaming service. 
/// </summary>
public class MicManager : StreamManager
{
    /// <summary>
    /// Structure that models the clips that are going to be streamed.
    /// </summary>
    private struct AudioStruc
    {
        /// <summary>
        /// Number of samples of the audio.
        /// </summary>
        public int samples;

        /// <summary>
        /// Number of channels of the audio.
        /// </summary>
        public int channels;

        /// <summary>
        /// Sampling frequency of the audio.
        /// </summary>
        public int frequency;

        /// <summary>
        /// Samples that from the audio.
        /// </summary>
        public float[] data;

        /// <summary>
        /// Number of samples correctly initialized.
        /// </summary>
        public int samplesReceived;

        /// <summary>
        /// Maximum size of the chunks that will be received to contruct this audio.
        /// </summary>
        public int chunkSize;

        /// <summary>
        /// Constructor of an audio structure.
        /// </summary>
        /// <param name="s">Number of samples of the audio.</param>
        /// <param name="c">Number of channels of the audio.</param>
        /// <param name="f">Sampling frequency of the audio.</param>
        /// <param name="chunk">Maximum size of the chunks that will be received to contruct this audio.</param>
        public AudioStruc(int s, int c, int f, int chunk)
        {
            samples = s;
            channels = c;
            frequency = f;
            data = new float[samples * channels];
            samplesReceived = 0;
            chunkSize = chunk;
        }
    }

    /// <summary>
    /// Message types used for sending audio clips.
    /// </summary>
    private class AudioMsgType : StreamMsgType
    {
        /// <summary>
        /// Constructor that inits the base values for the types.
        /// </summary>
        public AudioMsgType()
        {
            Header = "AudioHeader";
            Chunk = "AudioChunk";
        }

        public override void UpdateTypes(ulong n)
        {
            Header += n * 100;
            Chunk += n * 100;
        }
    }

    /// <summary>
    /// Header message used for sending audio clips.
    /// </summary>
    private class AudioHeaderMessage : StreamHeaderMessage
    {
        /// <summary>
        /// Number of samples of the audio.
        /// </summary>
        public int samples;

        /// <summary>
        /// Number of channels of the audio.
        /// </summary>
        public int channels;

        /// <summary>
        /// Sampling frequency of the audio.
        /// </summary>
        public int frequency;

        /// <summary>
        /// Constructor to initialize a header message for sending an audio clip.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="s">Number of samples of the audio.</param>
        /// <param name="c">Number of channels of the audio.</param>
        /// <param name="f">Sampling frequency of the audio.</param>
        /// <param name="chSz">Maximum size of the following chunks associated with this header.</param>
        public AudioHeaderMessage(ulong netId, uint id, int s, int c, int f, int chSz) : base(netId, id, chSz)
        {
            samples = s;
            channels = c;
            frequency = f;
        }

        public override void NetworkSerialize(NetworkSerializer serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.Serialize(ref samples);
            serializer.Serialize(ref channels);
            serializer.Serialize(ref frequency);
        }
    }

    /// <summary>
    /// Chunk message used for sending audio clips.
    /// </summary>
    private class AudioChunkMessage : StreamChunkMessage
    {
        /// <summary>
        /// Samples that from the audio.
        /// </summary>
        public float[] data;

        /// <summary>
        /// Constructor to initialize a chunk message for sending an audio clip.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="o">Position in the sequence of chunks with the same id. Starts in zero.</param>
        /// <param name="length">Maximum size of the chunk to be sent.</param>
        public AudioChunkMessage(ulong netId, uint id, int o, int length) : base(netId, id, o)
        {
            data = new float[length];
        }

        /// <summary>
        /// Constructor to initialize a chunk message for sending an audio clip.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="o">Position in the sequence of chunks with the same id. Starts in zero.</param>
        /// <param name="data">Chunk of samples of the clip.</param>
        /// <param name="size">Number of valid samples contained in this chunk.</param>
        public AudioChunkMessage(ulong netId, uint id, int o, float[] data, int size) : base(netId, id, o)
        {
            this.data = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                this.data[i] = data[i];
            }
            this.size = size;
        }

        public override void NetworkSerialize(NetworkSerializer serializer)
        {
            base.NetworkSerialize(serializer);
            for(int i = 0; i < size; i++)
            {
                serializer.Serialize(ref data[i]);
            }
        }
    }

    /// <summary>
    /// Clip that receives the data recorded by the microphone.
    /// </summary>
    private AudioClip voiceClip;

    /// <summary>
    /// Last clip that was received and must be played.
    /// </summary>
    private AudioClip lastCapturedClip;

    /// <summary>
    /// Sampling frequency for the audio recorded.
    /// </summary>
    [SerializeField] private int frequency = 44100;

    /// <summary>
    /// Message types used to send the clips of this instance.
    /// </summary>
    private AudioMsgType clipMsgType = new AudioMsgType();

    /// <summary>
    /// Data structure that constains and manages all the clip's data received
    /// through messages.
    /// </summary>
    private StreamMessageDataSupport
        <AudioStruc, AudioHeaderMessage, AudioChunkMessage>
        msgData = new StreamMessageDataSupport<AudioStruc, AudioHeaderMessage, AudioChunkMessage>();

    private void Start()
    {
        Initialize(msgData, clipMsgType);

        CreateHandlers(clipMsgType,
            OnAudioHeaderMessageFromClient,
            OnAudioChunkMessageFromClient,
            OnAudioHeaderMessageFromServer,
            OnAudioChunkMessageFromServer);
    }

    private void Update()
    {
        if (voiceClip != null)
        {
            BroadcastStream();
        }

        UpdateStream(msgData, RegenerateClipFromReceivedData);
        msgData.ManageUnheadedChunks();
    }

    /// <summary>
    /// Corutine that captures an audio clip from the microphone, obtains its
    /// relevant data and sends the corresponding header and chunk messages.
    /// </summary>
    /// <returns>IEnumerator to be run.</returns>
    protected override IEnumerator SendStream()
    {
        uint clipId = nextId;
        nextId += 1;
        Debug.Log("Sending clip with ID " + clipId + ".");
        //Texture2D texture = Paint(1000);
        float[] samplesData = new float[voiceClip.samples * voiceClip.channels];
        voiceClip.GetData(samplesData, 0);
        int size = Mathf.FloorToInt(samplesData.Length / Mathf.Ceil(samplesData.Length * (float)sizeof(float) / maxChunkSize));
        Debug.Log("Chunk size " + size);
        //lastCapturedClip = voiceClip;
        var headerMessage = new AudioHeaderMessage(networkIdentity.NetworkObjectId,
            clipId, voiceClip.samples, voiceClip.channels, frequency, size);
        SendHeaderMessage(clipMsgType, headerMessage);

        List<(int, float[], int)> chunks = DivideArrayInChunks(samplesData, size);
        foreach (var chunk in chunks)
        {
            var chunkMessage = new AudioChunkMessage(networkIdentity.NetworkObjectId, clipId, chunk.Item1, chunk.Item2, chunk.Item3);
            SendChunkMessage(clipMsgType, chunkMessage);
        }

        Debug.Log("Clip for ID " + clipId + " has been sent.");
        yield return new WaitForSeconds(0.01f);
    }

    /// <summary>
    /// Handler for header messages received on the server.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnAudioHeaderMessageFromClient(ulong sender, Stream msg)
    {
        using (PooledNetworkReader reader = PooledNetworkReader.Get(msg))
        {
            //var header = msg.ReadMessage<AudioHeaderMessage>();
            //OnStreamHeaderMessageFromClient(clipMsgType, header);
        }
    }

    /// <summary>
    /// Handler for chunk messages received on the server.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnAudioChunkMessageFromClient(ulong sender, Stream msg)
    {
        using (PooledNetworkReader reader = PooledNetworkReader.Get(msg))
        {
            //var row = msg.ReadMessage<AudioChunkMessage>();
            //OnStreamChunkMessageFromClient(clipMsgType, row);
        }
    }

    /// <summary>
    /// Handler for header messages received on the client.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnAudioHeaderMessageFromServer(ulong sender, Stream msg)
    {
        using (PooledNetworkReader reader = PooledNetworkReader.Get(msg))
        {
            //var header = msg.ReadMessage<AudioHeaderMessage>();
            //OnStreamHeaderMessageFromServer(header, OnAudioHeaderReceived);
        }
    }

    /// <summary>
    /// Handler for chunk messages received on the client.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnAudioChunkMessageFromServer(ulong sender, Stream msg)
    {
        using (PooledNetworkReader reader = PooledNetworkReader.Get(msg))
        {
            //var row = msg.ReadMessage<AudioChunkMessage>();
            //OnStreamChunkMessageFromServer(row, OnAudioChunkReceived);
        }
    }

    /// <summary>
    /// Processes the header messages received on the client.
    /// </summary>
    /// <param name="header">Clip's header message.</param>
    private void OnAudioHeaderReceived(AudioHeaderMessage header)
    {
        AudioStruc clipS = new AudioStruc(header.samples, header.channels, header.frequency, header.chunkSize);
        msgData.RecoverEarlyChunks(header, clipS, SaveChunk);
        if (msgData.CheckTimestamp(header))
        {
            StartCoroutine(
                msgData.WaitTillReceiveAllTheStream(
                    header,
                    (uint id) => msgData[id].samplesReceived < msgData[id].data.Length,
                    streamTimeout));
        }
    }

    /// <summary>
    /// Processes the chunk messages received on the client.
    /// </summary>
    /// <param name="row">Clip's chunk message.</param>
    private void OnAudioChunkReceived(AudioChunkMessage row)
    {
        msgData.AddChunk(row, SaveChunk);
    }

    /// <summary>
    /// Saves on the data structure the data contained in a chunk.
    /// </summary>
    /// <param name="row">Image's chunk message.</param>
    private void SaveChunk(AudioChunkMessage row)
    {
        AudioStruc clipStruc = msgData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && clipStruc.samplesReceived < clipStruc.data.Length; i++)
        {
            clipStruc.data[i + row.order * clipStruc.chunkSize] = row.data[i];
            clipStruc.samplesReceived += 1;
        }
        msgData[row.id] = clipStruc;
        Debug.Log(clipStruc.samplesReceived + "/" + clipStruc.data.Length
            + " samples currently recived for ID " + row.id + ".");
    }

    /// <summary>
    /// Regenerates a AudioClip from the complete audio structure stored
    /// on the data structure.
    /// </summary>
    /// <param name="id">Identifier of the clip received to regenerate.</param>
    private void RegenerateClipFromReceivedData(uint id)
    {
        Debug.Log("Regenerating clip from received data.");
        AudioStruc clipS = msgData[id];
        msgData.RemoveStream(id);
        AudioClip clip = AudioClip.Create("Received clip with ID " + id, clipS.samples, clipS.channels, clipS.frequency, false);
        clip.SetData(clipS.data, 0);
        lastCapturedClip = clip;
        
    }

    /// <summary>
    /// Returns the last recorded clip.
    /// </summary>
    /// <returns>AudioClip containing the last recorded audio.</returns>
    public AudioClip ObtainMicrophoneClip()
    {
        if (!IsLocalPlayer && lastCapturedClip != null)
        {
            return lastCapturedClip;
        }
        else
        {
            return null;
        }
    }

    public override void StartRecording()
    {
        if (IsLocalPlayer)
        {
            if (Microphone.devices.Length > 0)
            {
                Debug.Log("Turning mic on.");
                if (voiceClip == null)
                {
                    voiceClip = Microphone.Start(Microphone.devices[0], true, 1, frequency);
                    while (!(Microphone.GetPosition(Microphone.devices[0]) > 0)) { }
                }
            }
        }
        StreamIsOnServerRpc();
    }

    public override void StopRecording()
    {
        if (IsLocalPlayer)
        {
            if (Microphone.devices.Length > 0)
            {
                Debug.Log("Turning mic off.");
                Microphone.End(Microphone.devices[0]);
                if (voiceClip != null)
                {
                    voiceClip = null;
                }
            }
            lastCapturedClip = null;
        }
        StreamIsOffServerRpc();
    }

    private void OnDestroy()
    {
        DestroyHandlers(clipMsgType);
    }

    /// <summary>
    /// Notifies the sever that the stream is starting.
    /// </summary>
    [ServerRpc]
    private void StreamIsOnServerRpc()
    {
        StreamIsOnServer();
    }

    /// <summary>
    /// Notifies the sever that the stream has finished.
    /// </summary>
    [ServerRpc]
    private void StreamIsOffServerRpc()
    {
        StreamIsOffServer();
        StreamIsOffClientRpc();
    }

    /// <summary>
    /// Notifies all the clients that the stream has finished.
    /// </summary>
    [ClientRpc]
    private void StreamIsOffClientRpc()
    {
        msgData.RemoveAllStreams();
        lastCapturedClip = null;
    }
}
