using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class Texture2DSerializer
{
    /*public static void WriteTexture2D(this NetworkWriter writer, Texture2D texture) {
        Debug.Log("Writing");
        Color32[] data = texture.GetPixels32(0);
        writer.Write(texture.width);
        writer.Write(texture.height);
        writer.Write(data.Length);
        foreach (var pixel in data)
        {
            writer.Write(pixel);
        }
    }

    public static Texture2D ReadTexture2D(this NetworkReader reader) {
        Debug.Log("Reading");
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int dataLength = reader.ReadInt32();
        Texture2D texture = new Texture2D(width, height);
        Color32[] pixels = new Color32[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            pixels[i] = reader.ReadColor32();
        }
        texture.SetPixels32(pixels);
        texture.Apply(true);
        return texture;
    }*/
}
