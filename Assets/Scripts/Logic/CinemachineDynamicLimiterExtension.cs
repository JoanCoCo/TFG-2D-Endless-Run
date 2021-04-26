using UnityEngine;

namespace Cinemachine
{
    /// <summary>
    /// An add-on module for Cinemachine Virtual Camera that post-processes
    /// the final position of the virtual camera. Ensure the camera will be
    /// between the given limits.
    /// </summary>
    [DocumentationSorting(DocumentationSortingAttribute.Level.UserRef)]
    [ExecuteInEditMode]
    [AddComponentMenu("")] // Hide in menu
    [SaveDuringPlay]

    public class DynamicLimiter2D : CinemachineExtension
    {
        /// <summary>The Unity transform that represents the lowest X value for the camera position.</summary>
        [Tooltip("The Unity transform that represents the lowest X value for the camera position.")]
        public Transform leftLimit;
        /// <summary>The Unity transform that represents the highest X value for the camera position.</summary>
        [Tooltip("The Unity transform that represents the highest X value for the camera position.")]
        public Transform rightLimit;
        /// <summary>The Unity transform that represents the lowest Y value for the camera position.</summary>
        [Tooltip("The Unity transform that represents the lowest Y value for the camera position.")]
        public Transform upperLimit;
        /// <summary>The Unity transform that represents the highest Y value for the camera position.</summary>
        [Tooltip("The Unity transform that represents the highest Y value for the camera position.")]
        public Transform lowerLimit;

        /// <summary>Callcack to to the collision resolution and shot evaluation</summary>
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

        private void OnDrawGizmosSelected()
        {
            if (leftLimit != null && rightLimit != null && upperLimit != null && lowerLimit != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(leftLimit.position, 0.5f);
                Gizmos.DrawSphere(rightLimit.position, 0.5f);
                Gizmos.DrawSphere(upperLimit.position, 0.5f);
                Gizmos.DrawSphere(lowerLimit.position, 0.5f);
                Gizmos.DrawWireCube(new Vector3((rightLimit.position.x - leftLimit.position.x) * 0.5f + leftLimit.position.x,
                    (upperLimit.position.y - lowerLimit.position.y) * 0.5f + lowerLimit.position.y, 0.0f),
                    new Vector3((rightLimit.position.x - leftLimit.position.x),
                    (upperLimit.position.y - lowerLimit.position.y), 1.0f));
            }
        }
    }
}
