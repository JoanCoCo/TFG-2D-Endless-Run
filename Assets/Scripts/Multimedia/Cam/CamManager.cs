using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CamManager : DistributedEntityBehaviour
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

    private class TextureMsgType
    {
        public short Header = MsgType.Highest + 1;
        public short Chunk = MsgType.Highest + 2;

        public void UpdateTypes(int n)
        {
            Header += (short) (n * 10);
            Chunk += (short) (n * 10);
        }
    }

    private class TextureHeaderMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int width;
        public int height;
        public int chunkSize;
        public long timeStamp;

        public TextureHeaderMessage() { }

        public TextureHeaderMessage(uint netId, uint id, int w, int h, int s)
        {
            this.netId = netId;
            this.id = id;
            width = w;
            height = h;
            chunkSize = s;
            timeStamp = System.DateTime.Now.ToUniversalTime().Ticks;
        }
    }

    private class TextureChunkMessage : MessageBase
    {
        public uint netId;
        public uint id;
        public int order;
        public int size;
        public Color32[] data;

        public TextureChunkMessage() { }

        public TextureChunkMessage(uint netId, uint id, int o, int length)
        {
            this.netId = netId;
            this.id = id;
            order = o;
            data = new Color32[length];
            size = 0;
        }
    }

    private WebCamTexture cam;
    private Texture2D lastCapturedFrame;
    [SerializeField] private int framesPerSecond = 20;
    [SerializeField] private int resolution = 128;
    private float elapsedTime = 0.0f;

    private const int MAX_CHUNK_SIZE = 40000;

    private uint nextId = 0;

    private Dictionary<uint, bool> textWasFullyReceived = new Dictionary<uint, bool>();
    private Dictionary<uint, TextureStruc> textData = new Dictionary<uint, TextureStruc>();
    private KeySortedList<uint, long> textIdsReceived = new KeySortedList<uint, long>();
    private List<TextureChunkMessage> unheadedChunks = new List<TextureChunkMessage>();
    private Dictionary<uint, int> amountOfEarlyChunks = new Dictionary<uint, int>();

    [SerializeField] private float textureTimeout = 1.0f;

    private NetworkIdentity networkIdentity;

    private TextureMsgType textureMsgType = new TextureMsgType();

    private long lastShownTimestamp = System.DateTime.Now.ToUniversalTime().Ticks;

    private void Start()
    {
        networkIdentity = GetComponent<NetworkIdentity>();
        textureMsgType.UpdateTypes((short)networkIdentity.netId.Value);

        if (isServer)
        {
            Debug.Log("Registering server handlers.");
            if(!NetworkServer.handlers.ContainsKey(textureMsgType.Header))
            {
                NetworkServer.RegisterHandler(textureMsgType.Header, OnTextureHeaderMessageFromClient);
            }
            if(!NetworkServer.handlers.ContainsKey(textureMsgType.Chunk))
            {
                NetworkServer.RegisterHandler(textureMsgType.Chunk, OnTextureRowMessageFromClient);
            }
        }
        if(isClient)
        {
            Debug.Log("Registering client handlers.");
            if (!NetworkManager.singleton.client.handlers.ContainsKey(textureMsgType.Header))
            {
                NetworkManager.singleton.client.RegisterHandler(textureMsgType.Header, OnTextureHeaderMessageFromServer);
            }
            if (!NetworkManager.singleton.client.handlers.ContainsKey(textureMsgType.Chunk))
            {
                NetworkManager.singleton.client.RegisterHandler(textureMsgType.Chunk, OnTextureRowMessageFromServer);
            }
        }
        if (isLocalPlayer) StartRecording();
    }

    // Update is called once per frame
    private void Update()
    {
        if (cam != null && cam.isPlaying)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= 1.0f / framesPerSecond)
            {
                SendSnapShoot();
                elapsedTime = 0.0f;
            }
        }

        try
        {
            if (textIdsReceived.Count > 0)
            {
                if (textWasFullyReceived[textIdsReceived[0]])
                {
                    if(textIdsReceived.KeyAt(0) > lastShownTimestamp)
                    {
                        lastShownTimestamp = textIdsReceived.KeyAt(0);
                        Debug.Log("Last timestamp: " + lastShownTimestamp);
                        RegenerateTextureFromReceivedData(textIdsReceived[0]);
                    } else
                    {
                        RemoveTexture(textIdsReceived[0]);
                    }
                }
            }
        }
        catch 
        {
            textIdsReceived.RemoveAt(0);
        }
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
        int size = Mathf.FloorToInt(pixelData.Length / Mathf.Ceil(pixelData.Length * 4.0f / MAX_CHUNK_SIZE));
        Debug.Log("Chunk size " + size);
        var headerMessage = new TextureHeaderMessage(networkIdentity.netId.Value, textId, texture.width, texture.height, size);
        if (isServer)
        {
            NetworkServer.SendByChannelToReady(gameObject, textureMsgType.Header, headerMessage, 2);
            //NetworkServer.SendToReady(gameObject, TextureMsgType.Header, headerMessage);
        } else
        {
            NetworkManager.singleton.client.SendByChannel(textureMsgType.Header, headerMessage, 2);
            //NetworkManager.singleton.client.Send(TextureMsgType.Header, headerMessage);
        }
        int order = 0;
        var rowMessage = new TextureChunkMessage(networkIdentity.netId.Value, textId, order, size);
        for (int i = 0; i <= pixelData.Length; i++)
        {
            if(i - size * (order + 1) >= 0 || i == pixelData.Length)
            {
                if (isServer)
                {
                    NetworkServer.SendByChannelToReady(gameObject, textureMsgType.Chunk, rowMessage, 2);
                    //NetworkServer.SendToReady(gameObject, TextureMsgType.Chunk, rowMessage);
                }
                else
                {
                    NetworkManager.singleton.client.SendByChannel(textureMsgType.Chunk, rowMessage, 2);
                    //NetworkManager.singleton.client.Send(TextureMsgType.Chunk, rowMessage);
                }
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
        if (header.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received texture header on server.");
            NetworkServer.SendByChannelToReady(gameObject, textureMsgType.Header, header, 2);
        }
        //NetworkServer.SendToReady(gameObject, TextureMsgType.Header, header);
    }

    private void OnTextureRowMessageFromClient(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        if (row.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received texture row on server.");
            NetworkServer.SendByChannelToReady(gameObject, textureMsgType.Chunk, row, 2);
        }
        //NetworkServer.SendToReady(gameObject, TextureMsgType.Chunk, row);
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

    private void OnTextureRowMessageFromServer(NetworkMessage msg)
    {
        var row = msg.ReadMessage<TextureChunkMessage>();
        if(row.netId == networkIdentity.netId.Value)
        {
            Debug.Log("Received texture row on client.");
            OnTextureRowReceived(row);
        }
    }

    private void OnTextureHeaderReceived(TextureHeaderMessage header)
    {
        TextureStruc textS = new TextureStruc(header.width, header.height, header.chunkSize);
        textData[header.id] = textS;
        textWasFullyReceived[header.id] = false;
        if(amountOfEarlyChunks.ContainsKey(header.id))
        {
            int i = 0;
            int count = 0;
            while(i < amountOfEarlyChunks[header.id] && i < unheadedChunks.Count)
            {
                var row = unheadedChunks[i];
                if(row.id == header.id)
                {
                    SaveChunk(row);
                    unheadedChunks.RemoveAt(i);
                    count += 1;
                }
                i += 1;
            }
            if(amountOfEarlyChunks[header.id] != count)
            {
                RemoveTexture(header.id);
            }
            amountOfEarlyChunks.Remove(header.id);
        }
        if(textIdsReceived.Count > 0 && textIdsReceived.KeyAt(0) >= header.timeStamp)
        {
            RemoveTexture(header.id);
        } else
        {
            textIdsReceived.Add(header.id, header.timeStamp);
            StartCoroutine(WaitTillReceiveAllTheTexture(header.id));
        }
    }

    private void OnTextureRowReceived(TextureChunkMessage row)
    {
        if (textData.ContainsKey(row.id))
        {
            SaveChunk(row);
        } else
        {
            unheadedChunks.Add(row);
            if(amountOfEarlyChunks.ContainsKey(row.id))
            {
                amountOfEarlyChunks[row.id] += 1;
            } else
            {
                amountOfEarlyChunks[row.id] = 1;
            }
            Debug.Log(amountOfEarlyChunks[row.id] + " chunk(s) with our previous head received.");
        }
    }

    private void SaveChunk(TextureChunkMessage row)
    {
        TextureStruc textureStruc = textData[row.id];
        for (int i = 0; i < row.data.Length && i < row.size && textureStruc.pixelsReceived < textureStruc.data.Length; i++)
        {
            textureStruc.data[i + row.order * textureStruc.chunkSize] = row.data[i];
            textureStruc.pixelsReceived += 1;
        }
        textData[row.id] = textureStruc;
        Debug.Log(textureStruc.pixelsReceived + "/" + textureStruc.data.Length
            + " pixels currently recived for ID " + row.id + ".");
    }

    IEnumerator WaitTillReceiveAllTheTexture(uint id)
    {
        uint waitingId = id;
        float elapsedTime = 0;
        while(textData.ContainsKey(waitingId) && textData[waitingId].pixelsReceived < textData[waitingId].data.Length)
        {
            yield return new WaitForSecondsRealtime(0.01f);
            elapsedTime += 0.01f;
            if(elapsedTime > textureTimeout)
            {
                RemoveTexture(waitingId);
                yield break;
            }
        }
        if (textData.ContainsKey(waitingId) && elapsedTime <= textureTimeout)
        {
            textWasFullyReceived[waitingId] = true;
        } else
        {
            RemoveTexture(waitingId);
        }
    }

    private void RegenerateTextureFromReceivedData(uint id)
    {
        Debug.Log("Regenerating texture from received data.");
        TextureStruc textS = textData[id];
        RemoveTexture(id);
        Texture2D texture = new Texture2D(textS.width, textS.height);
        texture.SetPixels32(textS.data);
        texture.Apply(true);
        lastCapturedFrame = texture;
    }

    private void RemoveTexture(uint id)
    {
        if(textIdsReceived.Contains(id)) textIdsReceived.Remove(id);
        if(textWasFullyReceived.ContainsKey(id)) textWasFullyReceived.Remove(id);
        if(textData.ContainsKey(id)) textData.Remove(id);
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

    public void StartRecording()
    {
        Debug.Log("Turning camera on.");
        if (cam == null)
        {
            cam = new WebCamTexture();
        }

        if (cam != null && !cam.isPlaying)
        {
            cam.Play();
        }
    }

    private Texture2D Paint(int size)
    {
        int width = size;
        int height = size;
        Texture2D drawing = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        Color c = Random.ColorHSV();
        for (var i = 0; i < width * height; i++)
        {
            colors[i] = c;
        }
        drawing.SetPixels(colors);
        drawing.Apply(false);
        return drawing;
    }

    private void OnDestroy()
    {
        if (isServer)
        {
            if (NetworkServer.handlers.ContainsKey(textureMsgType.Header))
            {
                NetworkServer.UnregisterHandler(textureMsgType.Header);
            }
            if (NetworkServer.handlers.ContainsKey(textureMsgType.Chunk))
            {
                NetworkServer.UnregisterHandler(textureMsgType.Chunk);
            }
        }
        if (isClient)
        {
            if (NetworkManager.singleton.client.handlers.ContainsKey(textureMsgType.Header))
            {
                NetworkManager.singleton.client.UnregisterHandler(textureMsgType.Header);
            }
            if (NetworkManager.singleton.client.handlers.ContainsKey(textureMsgType.Chunk))
            {
                NetworkManager.singleton.client.UnregisterHandler(textureMsgType.Chunk);
            }
        }
    }
}
