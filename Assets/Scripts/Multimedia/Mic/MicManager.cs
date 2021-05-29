using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MicManager : StreamManager
{
    private struct AudioStruc
    {
        public int samples;
        public int channels;
        public int frequency;
        public float[] data;
        public int samplesReceived;
        public int chunkSize;

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

    private class AudioMsgType : StreamMsgType
    {
        public AudioMsgType()
        {
            Header = MsgType.Highest + 3;
            Chunk = MsgType.Highest + 4;
        }

        public override void UpdateTypes(int n)
        {
            Header += (short)(n * 100);
            Chunk += (short)(n * 100);
        }
    }

    private class AudioHeaderMessage : StreamHeaderMessage
    {
        public int samples;
        public int channels;
        public int frequency;

        public AudioHeaderMessage() : base() { }

        public AudioHeaderMessage(uint netId, uint id, int s, int c, int f, int chSz) : base(netId, id, chSz)
        {
            samples = s;
            channels = c;
            frequency = f;
        }

        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            writer.Write(samples);
            writer.Write(channels);
            writer.Write(frequency);
        }

        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            samples = reader.ReadInt32();
            channels = reader.ReadInt32();
            frequency = reader.ReadInt32();
        }
    }

    private class AudioChunkMessage : StreamChunkMessage
    {
        public float[] data;

        public AudioChunkMessage() : base() { }

        public AudioChunkMessage(uint netId, uint id, int o, int length) : base(netId, id, o)
        {
            data = new float[length];
        }

        public AudioChunkMessage(uint netId, uint id, int o, float[] data, int size) : base(netId, id, o)
        {
            this.data = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                this.data[i] = data[i];
            }
            this.size = size;
        }

        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            for(int i = 0; i < size; i++)
            {
                writer.Write(data[i]);
            }
        }

        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            data = new float[size];
            for(int i = 0; i < size; i++)
            {
                data[i] = reader.ReadSingle();
            }
        }
    }

    private AudioClip voiceClip;
    private AudioClip lastCapturedClip;

    [SerializeField] private int frequency = 44100;

    private AudioMsgType clipMsgType = new AudioMsgType();

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
        var headerMessage = new AudioHeaderMessage(networkIdentity.netId.Value,
            clipId, voiceClip.samples, voiceClip.channels, frequency, size);
        SendHeaderMessage(clipMsgType, headerMessage);

        List<(int, float[], int)> chunks = DivideArrayInChunks(samplesData, size);
        foreach (var chunk in chunks)
        {
            var chunkMessage = new AudioChunkMessage(networkIdentity.netId.Value, clipId, chunk.Item1, chunk.Item2, chunk.Item3);
            SendChunkMessage(clipMsgType, chunkMessage);
        }

        Debug.Log("Clip for ID " + clipId + " has been sent.");
        yield return new WaitForSeconds(0.01f);
    }

    private void OnAudioHeaderMessageFromClient(NetworkMessage msg)
    {
        var header = msg.ReadMessage<AudioHeaderMessage>();
        OnStreamHeaderMessageFromClient(clipMsgType, header);
    }

    private void OnAudioChunkMessageFromClient(NetworkMessage msg)
    {
        var row = msg.ReadMessage<AudioChunkMessage>();
        OnStreamChunkMessageFromClient(clipMsgType, row);
    }

    private void OnAudioHeaderMessageFromServer(NetworkMessage msg)
    {
        var header = msg.ReadMessage<AudioHeaderMessage>();
        OnStreamHeaderMessageFromServer(header, OnAudioHeaderReceived);
    }

    private void OnAudioChunkMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<AudioChunkMessage>();
        OnStreamChunkMessageFromServer(row, OnAudioChunkReceived);
    }

    private void OnAudioHeaderReceived(AudioHeaderMessage header)
    {
        AudioStruc clipS = new AudioStruc(header.samples, header.channels, header.frequency, header.chunkSize);
        msgData.RecoverEarlyChunks(header, clipS, SaveChunk);
        if (msgData.CheckTimestamp(header))
        {
            StartCoroutine(
                msgData.WaitTillReceiveAllTheStream(
                    header.id,
                    (uint id) => msgData[id].samplesReceived < msgData[id].data.Length,
                    streamTimeout));
        }
    }

    private void OnAudioChunkReceived(AudioChunkMessage row)
    {
        msgData.AddChunk(row, SaveChunk);
    }

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

    private void RegenerateClipFromReceivedData(uint id)
    {
        Debug.Log("Regenerating clip from received data.");
        AudioStruc clipS = msgData[id];
        msgData.RemoveStream(id);
        AudioClip clip = AudioClip.Create("Received clip with ID " + id, clipS.samples, clipS.channels, clipS.frequency, false);
        clip.SetData(clipS.data, 0);
        lastCapturedClip = clip;
        
    }

    public AudioClip ObtainMicrophoneClip()
    {
        if (!isLocalPlayer && lastCapturedClip != null)
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
        if (isLocalPlayer)
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
        CmdStreamIsOn();
    }

    public override void StopRecording()
    {
        if (isLocalPlayer)
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
        CmdStreamIsOff();
    }

    private void OnDestroy()
    {
        DestroyHandlers(clipMsgType);
    }

    [Command]
    protected void CmdStreamIsOn()
    {
        StreamIsOnServer();
    }

    [Command]
    protected void CmdStreamIsOff()
    {
        StreamIsOffServer();
        RpcStreamIsOff();
    }

    [ClientRpc]
    protected void RpcStreamIsOff()
    {
        msgData.RemoveAllStreams();
        lastCapturedClip = null;
    }
}
