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

        public TextureChunkMessage(uint netId, uint id, int o, Color32[] data, int size) : base(netId, id, o)
        {
            this.data = new Color32[data.Length];
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

    private StreamMessageDataSupport
        <TextureStruc, TextureHeaderMessage, TextureChunkMessage>
        msgData = new StreamMessageDataSupport<TextureStruc, TextureHeaderMessage, TextureChunkMessage>();

    private void Start()
    {
        Initialize(msgData, textureMsgType);

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
            BroadcastStream();
        }

        UpdateStream(msgData, RegenerateTextureFromReceivedData);
        msgData.ManageUnheadedChunks();
    }

    protected override IEnumerator SendStream()
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

        List<(int, Color32[], int)> chunks = DivideArrayInChunks(pixelData, size);
        foreach (var chunk in chunks)
        {
            var chunkMessage = new TextureChunkMessage(networkIdentity.netId.Value, textId, chunk.Item1, chunk.Item2, chunk.Item3);
            SendChunkMessage(textureMsgType, chunkMessage);
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
        OnStreamHeaderMessageFromServer(header, OnTextureHeaderReceived);
    }

    private void OnTextureChunkMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        OnStreamChunkMessageFromServer(row, OnTextureChunkReceived);
    }

    private void OnTextureHeaderReceived(TextureHeaderMessage header)
    {
        TextureStruc textS = new TextureStruc(header.width, header.height, header.chunkSize);
        msgData.RecoverEarlyChunks(header, textS, SaveChunk);
        if(msgData.CheckTimestamp(header))
        {
            StartCoroutine(
                msgData.WaitTillReceiveAllTheStream(
                    header.id,
                    (uint id) => msgData[id].pixelsReceived < msgData[id].data.Length,
                    streamTimeout));
        }
    }

    private void OnTextureChunkReceived(TextureChunkMessage row)
    {
        msgData.AddChunk(row, SaveChunk);
    }

    private void SaveChunk(TextureChunkMessage row)
    {
        TextureStruc textureStruc = msgData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && textureStruc.pixelsReceived < textureStruc.data.Length; i++)
        {
            textureStruc.data[i + row.order * textureStruc.chunkSize] = row.data[i];
            textureStruc.pixelsReceived += 1;
        }
        msgData[row.id] = textureStruc;
        Debug.Log(textureStruc.pixelsReceived + "/" + textureStruc.data.Length
            + " pixels currently recived for ID " + row.id + ".");
    }

    private void RegenerateTextureFromReceivedData(uint id)
    {
        Debug.Log("Regenerating texture from received data.");
        TextureStruc textS = msgData[id];
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
        lastCapturedFrame = null;
    }
}
