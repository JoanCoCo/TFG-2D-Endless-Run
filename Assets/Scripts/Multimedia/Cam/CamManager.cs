using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Class that implements a camera streaming service. 
/// </summary>
public class CamManager : StreamManager
{
    /// <summary>
    /// Structure that models the images that are going to be streamed.
    /// </summary>
    private struct TextureStruc
    {
        /// <summary>
        /// Width of the image.
        /// </summary>
        public int width;

        /// <summary>
        /// Height of the image.
        /// </summary>
        public int height;

        /// <summary>
        /// Pixels of the image.
        /// </summary>
        public Color32[] data;

        /// <summary>
        /// Number of pixels correctly initialized.
        /// </summary>
        public int pixelsReceived;

        /// <summary>
        /// Maximum size of the chunks that will be received to contruct this image.
        /// </summary>
        public int chunkSize;

        /// <summary>
        /// Constructor of an image structure.
        /// </summary>
        /// <param name="w">Width of the image.</param>
        /// <param name="h">Height of the image.</param>
        /// <param name="chunk">Maximum size of the chunks that will be received to contruct this image.</param>
        public TextureStruc(int w, int h, int chunk)
        {
            width = w;
            height = h;
            data = new Color32[w * h];
            pixelsReceived = 0;
            chunkSize = chunk;
        }
    }

    /// <summary>
    /// Message types used for sending images.
    /// </summary>
    private class TextureMsgType : StreamMsgType
    {
        /// <summary>
        /// Constructor that inits the base values for the types.
        /// </summary>
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

    /// <summary>
    /// Header message used for sending images.
    /// </summary>
    private class TextureHeaderMessage : StreamHeaderMessage
    {
        /// <summary>
        /// Width of the image.
        /// </summary>
        public int width;

        /// <summary>
        /// Height of the image.
        /// </summary>
        public int height;

        /// <summary>
        /// Empty constructor mandatory for using the class as message.
        /// </summary>
        public TextureHeaderMessage() : base() { }

        /// <summary>
        /// Constructor to initialize a header message for sending an image.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="w">Width of the image.</param>
        /// <param name="h">Height of the image.</param>
        /// <param name="s">Maximum size of the following chunks associated with this header.</param>
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

    /// <summary>
    /// Chunk message used for sending images.
    /// </summary>
    private class TextureChunkMessage : StreamChunkMessage
    {
        /// <summary>
        /// Chunk of pixels of the image.
        /// </summary>
        public Color32[] data;

        /// <summary>
        /// Empty constructor mandatory for using the class as message.
        /// </summary>
        public TextureChunkMessage() : base() { }

        /// <summary>
        /// Constructor to initialize a chunk message for sending an image.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="o">Position in the sequence of chunks with the same id. Starts in zero.</param>
        /// <param name="length">Maximum size of the chunk to be sent.</param>
        public TextureChunkMessage(uint netId, uint id, int o, int length) : base(netId, id, o)
        {
            data = new Color32[length];
        }

        /// <summary>
        /// Constructor to initialize a chunk message for sending an image.
        /// </summary>
        /// <param name="netId">Network identifier of the player object who sent the message.</param>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="o">Position in the sequence of chunks with the same id. Starts in zero.</param>
        /// <param name="data">Chunk of pixels of the image.</param>
        /// <param name="size">Number of valid pixels contained in this chunk.</param>
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

    /// <summary>
    /// Webcam texture to access to the device camera.
    /// </summary>
    private WebCamTexture cam;

    /// <summary>
    /// Last frame that was received and must be shown.
    /// </summary>
    private Texture2D lastCapturedFrame;

    /// <summary>
    /// Resolution of a side in pixels of the squared image we want to record.
    /// </summary>
    [SerializeField] private int resolution = 128;

    /// <summary>
    /// Message types used to send the images of this instance.
    /// </summary>
    private TextureMsgType textureMsgType = new TextureMsgType();

    /// <summary>
    /// Data structure that constains and manages all the image's data received
    /// through messages.
    /// </summary>
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

    private void Update()
    {
        if (cam != null && cam.isPlaying)
        {
            BroadcastStream();
        }

        UpdateStream(msgData, RegenerateTextureFromReceivedData);
        msgData.ManageUnheadedChunks();
    }

    /// <summary>
    /// Corutine that captures an image from the camera, obtains its relevant data
    /// and sends the corresponding header and chunk messages.
    /// </summary>
    /// <returns>IEnumerator to be run.</returns>
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

    /// <summary>
    /// Handler for header messages received on the server.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnTextureHeaderMessageFromClient(NetworkMessage msg)
    {
        var header = msg.ReadMessage<TextureHeaderMessage>();
        OnStreamHeaderMessageFromClient(textureMsgType, header);
    }

    /// <summary>
    /// Handler for chunk messages received on the server.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnTextureChunkMessageFromClient(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        OnStreamChunkMessageFromClient(textureMsgType, row);
    }

    /// <summary>
    /// Handler for header messages received on the client.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnTextureHeaderMessageFromServer(NetworkMessage msg)
    {
        var header = msg.ReadMessage<TextureHeaderMessage>();
        OnStreamHeaderMessageFromServer(header, OnTextureHeaderReceived);
    }

    /// <summary>
    /// Handler for chunk messages received on the client.
    /// </summary>
    /// <param name="msg">Network message received.</param>
    private void OnTextureChunkMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        OnStreamChunkMessageFromServer(row, OnTextureChunkReceived);
    }

    /// <summary>
    /// Processes the header messages received on the client.
    /// </summary>
    /// <param name="header">Image's header message.</param>
    private void OnTextureHeaderReceived(TextureHeaderMessage header)
    {
        TextureStruc textS = new TextureStruc(header.width, header.height, header.chunkSize);
        msgData.RecoverEarlyChunks(header, textS, SaveChunk);
        if(msgData.CheckTimestamp(header))
        {
            StartCoroutine(
                msgData.WaitTillReceiveAllTheStream(
                    header,
                    (uint id) => msgData[id].pixelsReceived < msgData[id].data.Length,
                    streamTimeout));
        }
    }

    /// <summary>
    /// Processes the chunk messages received on the client.
    /// </summary>
    /// <param name="row">Image's chunk message.</param>
    private void OnTextureChunkReceived(TextureChunkMessage row)
    {
        msgData.AddChunk(row, SaveChunk);
    }

    /// <summary>
    /// Saves on the data structure the data contained in a chunk.
    /// </summary>
    /// <param name="row">Image's chunk message.</param>
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

    /// <summary>
    /// Regenerates a Texture2D from the complete image structure stored
    /// on the data structure.
    /// </summary>
    /// <param name="id">Identifier of the image received to regenerate.</param>
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

    /// <summary>
    /// Returns the last captured image.
    /// </summary>
    /// <returns>Sprite containing the last captured image.</returns>
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

    /// <summary>
    /// Notifies the sever that the stream is starting.
    /// </summary>
    [Command]
    private void CmdStreamIsOn()
    {
        StreamIsOnServer();
    }

    /// <summary>
    /// Notifies the sever that the stream has finished.
    /// </summary>
    [Command]
    private void CmdStreamIsOff()
    {
        StreamIsOffServer();
        RpcStreamIsOff();
    }

    /// <summary>
    /// Notifies all the clients that the stream has finished.
    /// </summary>
    [ClientRpc]
    private void RpcStreamIsOff()
    {
        msgData.RemoveAllStreams();
        lastCapturedFrame = null;
    }
}
