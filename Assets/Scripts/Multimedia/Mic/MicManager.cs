using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MicManager : StreamManager, IMediaInputManager
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
            Header += (short)(n * 10);
            Chunk += (short)(n * 10);
        }
    }

    private class AudioHeaderMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int chunkSize;
        public long timeStamp;

        public int samples;
        public int channels;
        public int frequency;

        public AudioHeaderMessage() { }

        public AudioHeaderMessage(uint netId, uint id, int s, int c, int f, int chSz)
        {
            this.netId = netId;
            this.id = id;
            chunkSize = chSz;
            timeStamp = System.DateTime.Now.ToUniversalTime().Ticks;
            samples = s;
            channels = c;
            frequency = f;
        }
    }

    private class AudioChunkMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int order;
        public int size;
        public float[] data;

        public AudioChunkMessage() { }

        public AudioChunkMessage(uint netId, uint id, int o, int length)
        {
            this.netId = netId;
            this.id = id;
            order = o;
            data = new float[length];
            size = 0;
        }
    }

    private AudioClip voiceClip;
    private AudioClip lastCapturedClip;

    [SerializeField] private int frequency = 44100;

    private const int MAX_CHUNK_SIZE = short.MaxValue;

    private Dictionary<uint, bool> clipWasFullyReceived = new Dictionary<uint, bool>();
    private Dictionary<uint, AudioStruc> clipData = new Dictionary<uint, AudioStruc>();
    private KeySortedList<uint, long> clipIdsReceived = new KeySortedList<uint, long>();
    private List<(AudioChunkMessage, float)> unheadedChunks = new List<(AudioChunkMessage, float)>();
    private Dictionary<uint, int> amountOfEarlyChunks = new Dictionary<uint, int>();

    private AudioMsgType clipMsgType = new AudioMsgType();

    private bool isMicOn = false;

    //[SerializeField] private AudioSource audioSource;

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

        if (isMicOn)
        {
            try
            {
                if (clipIdsReceived.Count > 0)
                {
                    if (clipWasFullyReceived[clipIdsReceived[0]])
                    {
                        if (clipIdsReceived.KeyAt(0) > lastStreamTimestamp)
                        {
                            lastStreamTimestamp = clipIdsReceived.KeyAt(0);
                            Debug.Log("Last timestamp: " + lastStreamTimestamp);
                            RegenerateClipFromReceivedData(clipIdsReceived[0]);
                        }
                        else
                        {
                            RemoveClip(clipIdsReceived[0]);
                        }
                    }
                }
            }
            catch
            {
                clipIdsReceived.RemoveAt(0);
            }
        }

        if (unheadedChunks.Count > 0)
        {
            for (int i = 0; i < unheadedChunks.Count; i++)
            {
                var chunk = unheadedChunks[i];
                chunk.Item2 += Time.deltaTime;
                if (chunk.Item2 > pendingHeadersTimeout)
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

    private Coroutine SendSnapShoot() => StartCoroutine(SendClip());

    IEnumerator SendClip()
    {
        uint clipId = nextId;
        nextId += 1;
        Debug.Log("Sending clip with ID " + clipId + ".");
        //Texture2D texture = Paint(1000);
        float[] samplesData = new float[voiceClip.samples * voiceClip.channels];
        voiceClip.GetData(samplesData, 0);
        int size = Mathf.FloorToInt(samplesData.Length / Mathf.Ceil(samplesData.Length * (float)sizeof(float) / MAX_CHUNK_SIZE));
        Debug.Log("Chunk size " + size);
        //lastCapturedClip = voiceClip;
        var headerMessage = new AudioHeaderMessage(networkIdentity.netId.Value,
            clipId, voiceClip.samples, voiceClip.channels, frequency, size);
        if (isServer)
        {
            NetworkServer.SendByChannelToReady(gameObject, clipMsgType.Header, headerMessage, 2);
            //NetworkServer.SendToReady(gameObject, TextureMsgType.Header, headerMessage);
        }
        else
        {
            NetworkManager.singleton.client.SendByChannel(clipMsgType.Header, headerMessage, 2);
            //NetworkManager.singleton.client.Send(TextureMsgType.Header, headerMessage);
        }
        int order = 0;
        var rowMessage = new AudioChunkMessage(networkIdentity.netId.Value, clipId, order, size);
        for (int i = 0; i <= samplesData.Length; i++)
        {
            if (i - size * (order + 1) >= 0 || i == samplesData.Length)
            {
                if (isServer)
                {
                    NetworkServer.SendByChannelToReady(gameObject, clipMsgType.Chunk, rowMessage, 2);
                    //NetworkServer.SendToReady(gameObject, TextureMsgType.Chunk, rowMessage);
                }
                else
                {
                    NetworkManager.singleton.client.SendByChannel(clipMsgType.Chunk, rowMessage, 2);
                    //NetworkManager.singleton.client.Send(TextureMsgType.Chunk, rowMessage);
                }
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
        if (header.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received clip header on server.");
            NetworkServer.SendByChannelToReady(gameObject, clipMsgType.Header, header, 2);
        }
        //NetworkServer.SendToReady(gameObject, TextureMsgType.Header, header);
    }

    private void OnAudioChunkMessageFromClient(NetworkMessage msg)
    {
        var row = msg.ReadMessage<AudioChunkMessage>();
        if (row.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received clip row on server.");
            NetworkServer.SendByChannelToReady(gameObject, clipMsgType.Chunk, row, 2);
        }
        //NetworkServer.SendToReady(gameObject, TextureMsgType.Chunk, row);
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
        clipData[header.id] = textS;
        clipWasFullyReceived[header.id] = false;
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
                RemoveClip(header.id);
            }
            amountOfEarlyChunks.Remove(header.id);
        }
        if (clipIdsReceived.Count > 0 && clipIdsReceived.KeyAt(0) >= header.timeStamp)
        {
            RemoveClip(header.id);
        }
        else
        {
            clipIdsReceived.Add(header.id, header.timeStamp);
            StartCoroutine(WaitTillReceiveAllTheClip(header.id));
        }
    }

    private void OnAudioChunkReceived(AudioChunkMessage row)
    {
        if (clipData.ContainsKey(row.id))
        {
            SaveChunk(row);
        }
        else
        {
            unheadedChunks.Add((row, 0.0f));
            if (amountOfEarlyChunks.ContainsKey(row.id))
            {
                amountOfEarlyChunks[row.id] += 1;
            }
            else
            {
                amountOfEarlyChunks[row.id] = 1;
            }
            Debug.Log(amountOfEarlyChunks[row.id] + " chunk(s) with our previous head received.");
        }
    }

    private void SaveChunk(AudioChunkMessage row)
    {
        AudioStruc clipStruc = clipData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && clipStruc.samplesReceived < clipStruc.data.Length; i++)
        {
            clipStruc.data[i + row.order * clipStruc.chunkSize] = row.data[i];
            clipStruc.samplesReceived += 1;
        }
        clipData[row.id] = clipStruc;
        Debug.Log(clipStruc.samplesReceived + "/" + clipStruc.data.Length
            + " samples currently recived for ID " + row.id + ".");
    }

    IEnumerator WaitTillReceiveAllTheClip(uint id)
    {
        uint waitingId = id;
        float elapsedTime = 0;
        while (clipData.ContainsKey(waitingId) && clipData[waitingId].samplesReceived < clipData[waitingId].data.Length)
        {
            yield return new WaitForSecondsRealtime(0.01f);
            elapsedTime += 0.01f;
            if (elapsedTime > streamTimeout)
            {
                RemoveClip(waitingId);
                yield break;
            }
        }
        if (clipData.ContainsKey(waitingId) && elapsedTime <= streamTimeout)
        {
            clipWasFullyReceived[waitingId] = true;
        }
        else
        {
            RemoveClip(waitingId);
        }
    }

    private void RegenerateClipFromReceivedData(uint id)
    {
        Debug.Log("Regenerating clip from received data.");
        AudioStruc clipS = clipData[id];
        RemoveClip(id);
        AudioClip clip = AudioClip.Create("Received clip with ID " + id, clipS.samples, clipS.channels, clipS.frequency, false);
        clip.SetData(clipS.data, 0);
        lastCapturedClip = clip;
        
    }

    private void RemoveClip(uint id)
    {
        if (clipIdsReceived.Contains(id)) clipIdsReceived.Remove(id);
        if (clipWasFullyReceived.ContainsKey(id)) clipWasFullyReceived.Remove(id);
        if (clipData.ContainsKey(id)) clipData.Remove(id);
        if (amountOfEarlyChunks.ContainsKey(id)) amountOfEarlyChunks.Remove(id);
    }

    public AudioClip ObtainMicrophoneClip()
    {
        if (lastCapturedClip != null)
        {
            return lastCapturedClip;
        }
        else
        {
            return null;
        }
    }

    public void StartRecording()
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
        CmdMicIsOn();
    }

    public void StopRecording()
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
        CmdMicIsOff();
    }

    private void OnDestroy()
    {
        DestroyHandlers(clipMsgType);
    }

    [ClientRpc]
    private void RpcMicIsOff()
    {
        //isMicOn = false;
        for (int i = 0; i < clipIdsReceived.Count; i++)
        {
            RemoveClip(clipIdsReceived[0]);
        }
        lastCapturedClip = null;
    }

    /*[ClientRpc]
    private void RpcMicIsOn()
    {
        isMicOn = true;
    }*/

    [Command]
    private void CmdMicIsOn()
    {
        //RpcMicIsOn();
        isMicOn = true;
    }

    [Command]
    private void CmdMicIsOff()
    {
        isMicOn = false;
        RpcMicIsOff();
    }
}
