using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class DynamicDelimiter2D : CinemachineExtension
{
    public Transform leftLimit;
    public Transform rightLimit;
    public Transform upperLimit;
    public Transform lowerLimit;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        // Move the body before the Aim is calculated
        if (stage == CinemachineCore.Stage.Body)
        {
            //Vector3 displacement = RespectCameraRadius(state.RawPosition);
            //state.PositionCorrection += displacement;
            if (leftLimit != null && rightLimit != null && upperLimit != null && lowerLimit != null)
            {
                float cameraHeight = state.Lens.OrthographicSize * 2.0f;
                float cameraWidth = cameraHeight * state.Lens.Aspect;

                if (state.RawPosition.x - cameraWidth * 0.5f < leftLimit.position.x)
                {
                    state.PositionCorrection += Vector3.right * Mathf.Abs(state.RawPosition.x - cameraWidth * 0.5f - leftLimit.position.x);
                }
                else if (state.RawPosition.x + cameraWidth * 0.5f > rightLimit.position.x)
                {
                    state.PositionCorrection += Vector3.left * Mathf.Abs(state.RawPosition.x + cameraWidth * 0.5f - rightLimit.position.x);
                }

                if (state.RawPosition.y - cameraHeight * 0.5f < lowerLimit.position.y)
                {
                    state.PositionCorrection += Vector3.up * Mathf.Abs(state.RawPosition.y - cameraHeight * 0.5f - lowerLimit.position.y);
                }
                else if (state.RawPosition.y + cameraHeight * 0.5f > upperLimit.position.y)
                {
                    state.PositionCorrection += Vector3.down * Mathf.Abs(state.RawPosition.y + cameraHeight * 0.5f - upperLimit.position.y);
                }
            }
        }
    }
}
