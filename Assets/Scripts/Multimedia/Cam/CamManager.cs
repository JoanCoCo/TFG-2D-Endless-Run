using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CamManager : StreamManager
{
    private struct TextureStruc
    {
        public int width;
        public int height;
        public Color32[] data;
        public int pixelsReceived;
        public int chunkSize;

        public TextureStruc(int w, int h, int chunk)
        {
            width = w;
            height = h;
            data = new Color32[w * h];
            pixelsReceived = 0;
            chunkSize = chunk;
        }
    }

    private class TextureMsgType : StreamMsgType
    {
        public TextureMsgType()
        {
            Header = MsgType.Highest + 1;
            Chunk = MsgType.Highest + 2;
        }

        public override void UpdateTypes(int n)
        {
            Header += (short) (n * 100);
            Chunk += (short) (n * 100);
        }
    }

    private class TextureHeaderMessage : StreamHeaderMessage
    {
        public int width;
        public int height;

        public TextureHeaderMessage() : base() { }

        public TextureHeaderMessage(uint netId, uint id, int w, int h, int s) : base(netId, id, s)
        {
            width = w;
            height = h;
        }

        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            writer.Write(width);
            writer.Write(height);
        }

        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            width = reader.ReadInt32();
            height = reader.ReadInt32();
        }
    }

    private class TextureChunkMessage : StreamChunkMessage
    {
        public Color32[] data;

        public TextureChunkMessage() : base() { }

        public TextureChunkMessage(uint netId, uint id, int o, int length) : base(netId, id, o)
        {
            data = new Color32[length];
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
            data = new Color32[size];
            for(int i = 0; i < size; i++)
            {
                data[i] = reader.ReadColor32();
            }
        }
    }

    private WebCamTexture cam;
    private Texture2D lastCapturedFrame;
    [SerializeField] private int resolution = 128;

    private TextureMsgType textureMsgType = new TextureMsgType();

    private StreamMessageDataSupport<TextureStruc, TextureChunkMessage> msgData = new StreamMessageDataSupport<TextureStruc, TextureChunkMessage>();

    private void Start()
    {
        networkIdentity = GetComponent<NetworkIdentity>();
        textureMsgType.UpdateTypes((int)networkIdentity.netId.Value);

        CreateHandlers(textureMsgType,
            OnTextureHeaderMessageFromClient,
            OnTextureChunkMessageFromClient,
            OnTextureHeaderMessageFromServer,
            OnTextureChunkMessageFromServer);
    }

    // Update is called once per frame
    private void Update()
    {
        if (cam != null && cam.isPlaying)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= 1.0f / transmissionsPerSecond)
            {
                SendSnapShoot();
                elapsedTime = 0.0f;
            }
        }

        UpdateStream(msgData, RegenerateTextureFromReceivedData);
        ManageUnheadedChunks(msgData);
    }

    private Coroutine SendSnapShoot() => StartCoroutine(SendTexture());

    IEnumerator SendTexture()
    {
        uint textId = nextId;
        nextId += 1;
        Debug.Log("Sending snapshoot with ID " + textId + ".");
        //Texture2D texture = Paint(1000);
        int minTexSide = Mathf.Min(cam.width, cam.height);
        int x = minTexSide == cam.width ? 0 : (cam.width - minTexSide) / 2;
        int y = minTexSide == cam.height ? 0 : (cam.height - minTexSide) / 2;
        Texture2D texture = new Texture2D(minTexSide, minTexSide);
        texture.SetPixels(cam.GetPixels(x, y, minTexSide, minTexSide));
        texture.Apply(true);
        TextureScale.Bilinear(texture, resolution, resolution);
        //lastCapturedFrame = texture;
        Color32[] pixelData = texture.GetPixels32(0);
        int size = Mathf.FloorToInt(pixelData.Length / Mathf.Ceil(pixelData.Length * 4.0f / maxChunkSize));
        Debug.Log("Chunk size " + size);
        var headerMessage = new TextureHeaderMessage(networkIdentity.netId.Value, textId, texture.width, texture.height, size);
        SendHeaderMessage(textureMsgType, headerMessage);
        int order = 0;
        var rowMessage = new TextureChunkMessage(networkIdentity.netId.Value, textId, order, size);
        for (int i = 0; i <= pixelData.Length; i++)
        {
            if(i - size * (order + 1) >= 0 || i == pixelData.Length)
            {
                SendChunkMessage(textureMsgType, rowMessage);
                if (i == pixelData.Length) break;
                order += 1;
                rowMessage = new TextureChunkMessage(networkIdentity.netId.Value, textId, order, size);
            }
            rowMessage.data[i - size * order] = pixelData[i];
            rowMessage.size += 1;
        }

        Debug.Log("SnapShoot for ID " + textId + " has been sent.");
        yield return new WaitForSeconds(0.01f);
    }

    private void OnTextureHeaderMessageFromClient(NetworkMessage msg)
    {
        var header = msg.ReadMessage<TextureHeaderMessage>();
        OnStreamHeaderMessageFromClient(textureMsgType, header);
    }

    private void OnTextureChunkMessageFromClient(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        OnStreamChunkMessageFromClient(textureMsgType, row);
    }

    private void OnTextureHeaderMessageFromServer(NetworkMessage msg)
    {
        var header = msg.ReadMessage<TextureHeaderMessage>();
        if(header.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received texture header on client.");
            OnTextureHeaderReceived(header);
        }
    }

    private void OnTextureChunkMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        if(row.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received texture row on client.");
            OnTextureChunkReceived(row);
        }
    }

    private void OnTextureHeaderReceived(TextureHeaderMessage header)
    {
        TextureStruc textS = new TextureStruc(header.width, header.height, header.chunkSize);
        msgData.streamData[header.id] = textS;
        msgData.streamWasFullyReceived[header.id] = false;
        if(msgData.amountOfEarlyChunks.ContainsKey(header.id))
        {
            int i = 0;
            int count = 0;
            while(i < msgData.amountOfEarlyChunks[header.id] && i < msgData.unheadedChunks.Count)
            {
                var row = msgData.unheadedChunks[i].Item1;
                if(row.id == header.id)
                {
                    SaveChunk(row);
                    msgData.unheadedChunks.RemoveAt(i);
                    count += 1;
                }
                i += 1;
            }
            if(msgData.amountOfEarlyChunks[header.id] != count)
            {
                msgData.RemoveStream(header.id);
            }
            msgData.amountOfEarlyChunks.Remove(header.id);
        }
        if(msgData.streamIdsReceived.Count > 0 && msgData.streamIdsReceived.KeyAt(0) >= header.timeStamp)
        {
            msgData.RemoveStream(header.id);
        } else
        {
            msgData.streamIdsReceived.Add(header.id, header.timeStamp);
            StartCoroutine(WaitTillReceiveAllTheTexture(header.id));
        }
    }

    private void OnTextureChunkReceived(TextureChunkMessage row)
    {
        if (msgData.streamData.ContainsKey(row.id))
        {
            SaveChunk(row);
        } else
        {
            msgData.unheadedChunks.Add((row, 0.0f));
            if(msgData.amountOfEarlyChunks.ContainsKey(row.id))
            {
                msgData.amountOfEarlyChunks[row.id] += 1;
            } else
            {
                msgData.amountOfEarlyChunks[row.id] = 1;
            }
            Debug.Log(msgData.amountOfEarlyChunks[row.id] + " chunk(s) with our previous head received.");
        }
    }

    private void SaveChunk(TextureChunkMessage row)
    {
        TextureStruc textureStruc = msgData.streamData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && textureStruc.pixelsReceived < textureStruc.data.Length; i++)
        {
            textureStruc.data[i + row.order * textureStruc.chunkSize] = row.data[i];
            textureStruc.pixelsReceived += 1;
        }
        msgData.streamData[row.id] = textureStruc;
        Debug.Log(textureStruc.pixelsReceived + "/" + textureStruc.data.Length
            + " pixels currently recived for ID " + row.id + ".");
    }

    IEnumerator WaitTillReceiveAllTheTexture(uint id)
    {
        uint waitingId = id;
        float elapsedTime = 0;
        while(msgData.streamData.ContainsKey(waitingId) && msgData.streamData[waitingId].pixelsReceived < msgData.streamData[waitingId].data.Length)
        {
            yield return new WaitForSecondsRealtime(0.01f);
            elapsedTime += 0.01f;
            if(elapsedTime > streamTimeout)
            {
                msgData.RemoveStream(waitingId);
                yield break;
            }
        }
        if (msgData.streamData.ContainsKey(waitingId) && elapsedTime <= streamTimeout)
        {
            msgData.streamWasFullyReceived[waitingId] = true;
        } else
        {
            msgData.RemoveStream(waitingId);
        }
    }

    private void RegenerateTextureFromReceivedData(uint id)
    {
        Debug.Log("Regenerating texture from received data.");
        TextureStruc textS = msgData.streamData[id];
        msgData.RemoveStream(id);
        Texture2D texture = new Texture2D(textS.width, textS.height);
        texture.SetPixels32(textS.data);
        texture.Apply(true);
        lastCapturedFrame = texture;
    }

    public Sprite ObtainWebcamImage()
    {
        if(lastCapturedFrame != null)
        {
            Sprite spr = Sprite.Create(lastCapturedFrame,
                new Rect(0.0f, 0.0f, lastCapturedFrame.width, lastCapturedFrame.height),
                new Vector2(0.5f, 0.5f));
            return spr;
        } else
        {
            return null;
        }
    }

    public override void StartRecording()
    {
        if (isLocalPlayer)
        {
            Debug.Log("Turning camera on.");
            if (cam == null)
            {
                cam = new WebCamTexture();
                cam.requestedFPS = transmissionsPerSecond + 5;
            }

            if (cam != null && !cam.isPlaying)
            {
                cam.Play();
            }
        }
        CmdStreamIsOn();
    }

    public override void StopRecording()
    {
        if (isLocalPlayer)
        {
            Debug.Log("Turning camera off");
            if (cam != null && cam.isPlaying)
            {
                cam.Stop();
            }
        }
        lastCapturedFrame = null;
        CmdStreamIsOff();
    }

    private void OnDestroy()
    {
        DestroyHandlers(textureMsgType);
    }

    protected override void StreamIsOff()
    {
        msgData.RemoveAllStreams();
        lastCapturedFrame = null;
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
