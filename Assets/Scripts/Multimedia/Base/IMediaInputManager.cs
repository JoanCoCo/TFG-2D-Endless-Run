using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface of a class capable of streaming a media through the network.
/// </summary>
public interface IMediaInputManager
{
    /// <summary>
    /// Starts capturing and transmiting the media.
    /// </summary>
    public void StartRecording();

    /// <summary>
    /// Stops capturing and transmiting the media.
    /// </summary>
    public void StopRecording();
}
