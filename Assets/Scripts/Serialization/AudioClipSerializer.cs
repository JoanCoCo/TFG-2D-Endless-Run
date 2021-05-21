using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class AudioClipSerializer
{
    public static void WriteTexture2D(this NetworkWriter writer, AudioClip audio) {
        Debug.Log("Writing Audio Clip");
        writer.Write(audio.samples);
        writer.Write(audio.channels);
        writer.Write(audio.frequency);
        float[] samples = new float[audio.samples * audio.channels];
        audio.GetData(samples, 0);
        for (var i = 0; i < samples.Length; i++)
        {
            writer.Write(samples[i]);
        }
    }

    public static AudioClip ReadTexture2D(this NetworkReader reader) {
        Debug.Log("Reading Audio Clip");
        AudioClip audio = AudioClip.Create("Received AudioClip", reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), false);
        float[] samples = new float[audio.samples * audio.channels];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = reader.ReadSingle();
        }
        audio.SetData(samples, 0);
        return audio;
    }
}
