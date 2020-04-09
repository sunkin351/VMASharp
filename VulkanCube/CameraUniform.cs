using System;
using System.Numerics;
using VMASharp;

namespace VulkanCube
{
    public class CameraUniform
    {
        private Matrix4x4 Projection, View, Model, Clip, MVP;
        private bool Initialized = false;

        public CameraUniform()
        {
            Projection = Matrix4x4.Identity;
            View = Matrix4x4.Identity;
            Model = Matrix4x4.Identity;
            Clip = Matrix4x4.Identity;
        }

        public ref readonly Matrix4x4 MVPMatrix { get => ref MVP; }

        public void PerspectiveDegrees(float fieldOfViewDegrees, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        {
            Perspective(MathF.PI / 180 * fieldOfViewDegrees, aspectRatio, nearPlaneDistance, farPlaneDistance);
        }

        public void Perspective(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        {
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance);
        }

        public void LookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        {
            View = Matrix4x4.CreateLookAt(cameraPosition, cameraTarget, cameraUpVector);
        }

        public void SetModel(in Matrix4x4 model)
        {
            Model = model;
        }

        public void SetClip(in Matrix4x4 clip)
        {
            Clip = clip;
        }

        public void UpdateMVP()
        {
            MVP = Projection * View * Model * Clip;
        }
    }
}
