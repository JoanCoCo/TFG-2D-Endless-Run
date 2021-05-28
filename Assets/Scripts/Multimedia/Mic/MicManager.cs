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

    private StreamMessageDataSupport<AudioStruc, AudioChunkMessage> msgData = new StreamMessageDataSupport<AudioStruc, AudioChunkMessage>();

    private void Start()
    {
        networkIdentity = GetComponent<NetworkIdentity>();
        clipMsgType.UpdateTypes((int)networkIdentity.netId.Value);

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
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= 1.0f / transmissionsPerSecond)
            {
                SendSnapShoot();
                elapsedTime = 0.0f;
            }
        }

        UpdateStream(msgData, RegenerateClipFromReceivedData);
        ManageUnheadedChunks(msgData);
    }

    private Coroutine SendSnapShoot() => StartCoroutine(SendClip());

    IEnumerator SendClip()
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
        int order = 0;
        var rowMessage = new AudioChunkMessage(networkIdentity.netId.Value, clipId, order, size);
        for (int i = 0; i <= samplesData.Length; i++)
        {
            if (i - size * (order + 1) >= 0 || i == samplesData.Length)
            {
                SendChunkMessage(clipMsgType, rowMessage);
                if (i == samplesData.Length) break;
                order += 1;
                rowMessage = new AudioChunkMessage(networkIdentity.netId.Value, clipId, order, size);
            }
            rowMessage.data[i - size * order] = samplesData[i];
            rowMessage.size += 1;
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
        if (header.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received clip header on client.");
            OnAudioHeaderReceived(header);
        }
    }

    private void OnAudioChunkMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<AudioChunkMessage>();
        if (row.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received clip row on client.");
            OnAudioChunkReceived(row);
        }
    }

    private void OnAudioHeaderReceived(AudioHeaderMessage header)
    {
        AudioStruc textS = new AudioStruc(header.samples, header.channels, header.frequency, header.chunkSize);
        msgData.streamData[header.id] = textS;
        msgData.streamWasFullyReceived[header.id] = false;
        if (msgData.amountOfEarlyChunks.ContainsKey(header.id))
        {
            int i = 0;
            int count = 0;
            while (i < msgData.amountOfEarlyChunks[header.id] && i < msgData.unheadedChunks.Count)
            {
                var row = msgData.unheadedChunks[i].Item1;
                if (row.id == header.id)
                {
                    SaveChunk(row);
                    msgData.unheadedChunks.RemoveAt(i);
                    count += 1;
                }
                i += 1;
            }
            if (msgData.amountOfEarlyChunks[header.id] != count)
            {
                msgData.RemoveStream(header.id);
            }
            msgData.amountOfEarlyChunks.Remove(header.id);
        }
        if (msgData.streamIdsReceived.Count > 0 && msgData.streamIdsReceived.KeyAt(0) >= header.timeStamp)
        {
            msgData.RemoveStream(header.id);
        }
        else
        {
            msgData.streamIdsReceived.Add(header.id, header.timeStamp);
            StartCoroutine(WaitTillReceiveAllTheClip(header.id));
        }
    }

    private void OnAudioChunkReceived(AudioChunkMessage row)
    {
        if (msgData.streamData.ContainsKey(row.id))
        {
            SaveChunk(row);
        }
        else
        {
            msgData.unheadedChunks.Add((row, 0.0f));
            if (msgData.amountOfEarlyChunks.ContainsKey(row.id))
            {
                msgData.amountOfEarlyChunks[row.id] += 1;
            }
            else
            {
                msgData.amountOfEarlyChunks[row.id] = 1;
            }
            Debug.Log(msgData.amountOfEarlyChunks[row.id] + " chunk(s) with our previous head received.");
        }
    }

    private void SaveChunk(AudioChunkMessage row)
    {
        AudioStruc clipStruc = msgData.streamData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && clipStruc.samplesReceived < clipStruc.data.Length; i++)
        {
            clipStruc.data[i + row.order * clipStruc.chunkSize] = row.data[i];
            clipStruc.samplesReceived += 1;
        }
        msgData.streamData[row.id] = clipStruc;
        Debug.Log(clipStruc.samplesReceived + "/" + clipStruc.data.Length
            + " samples currently recived for ID " + row.id + ".");
    }

    IEnumerator WaitTillReceiveAllTheClip(uint id)
    {
        uint waitingId = id;
        float elapsedTime = 0;
        while (msgData.streamData.ContainsKey(waitingId) && msgData.streamData[waitingId].samplesReceived < msgData.streamData[waitingId].data.Length)
        {
            yield return new WaitForSecondsRealtime(0.01f);
            elapsedTime += 0.01f;
            if (elapsedTime > streamTimeout)
            {
                msgData.RemoveStream(waitingId);
                yield break;
            }
        }
        if (msgData.streamData.ContainsKey(waitingId) && elapsedTime <= streamTimeout)
        {
            msgData.streamWasFullyReceived[waitingId] = true;
        }
        else
        {
            msgData.RemoveStream(waitingId);
        }
    }

    private void RegenerateClipFromReceivedData(uint id)
    {
        Debug.Log("Regenerating clip from received data.");
        AudioStruc clipS = msgData.streamData[id];
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

    protected override void StreamIsOff()
    {
        msgData.RemoveAllStreams();
        lastCapturedClip = null;
    }

    [Command]
    protected void CmdStreamIsOn()
    {
        //RpcCameraIsOn();
        Debug.Log("Stream is on.");
        isStreamOn = true;
    }

    [Command]
    protected void CmdStreamIsOff()
    {
        isStreamOn = false;
        Debug.Log("Stream is off.");
        RpcStreamIsOff();
    }

    [ClientRpc]
    protected void RpcStreamIsOff()
    {
        //isCameraOn = false;
        StreamIsOff();
    }
}
