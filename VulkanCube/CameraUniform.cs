using System;
using System.Numerics;
using VMASharp;

namespace VulkanCube
{
    public class CameraUniform
    {
        //This matrix handles Vulkan's inverted Y and half Z coordinate system
        private static readonly Matrix4x4 VulkanClip = new Matrix4x4(1.0f,  0.0f,  0.0f,  0.0f,
                                                                     0.0f, -1.0f,  0.0f,  0.0f,
                                                                     0.0f,  0.0f,  0.5f,  0.0f,
                                                                     0.0f,  0.0f,  0.5f,  1.0f);

        private Matrix4x4 Projection, View, MVP;

        public CameraUniform()
        {
            Projection = Matrix4x4.Identity;
            View = Matrix4x4.Identity;
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
            View = glmCreateLookAt(cameraPosition, cameraTarget, cameraUpVector);
        }

        public void UpdateMVP()
        {
            MVP = View * Projection * VulkanClip;
        }

        //Necessary because Matrix4x4.CreateLookAt() does not properly handle translation
        //Ported from the GLM Mathematics library
        private static Matrix4x4 glmCreateLookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        {
            var f = Vector3.Normalize(cameraTarget - cameraPosition);
            var s = Vector3.Normalize(Vector3.Cross(f, cameraUpVector));
            var u = Vector3.Cross(s, f);

            Matrix4x4 result = Matrix4x4.Identity;

            result.M11 = s.X;
            result.M12 = s.Y;
            result.M13 = s.Z;

            result.M21 = u.X;
            result.M22 = u.Y;
            result.M23 = u.Z;

            result.M31 = -f.X;
            result.M32 = -f.Y;
            result.M33 = -f.Z;

            result.M41 = -Vector3.Dot(s, cameraPosition);
            result.M42 = -Vector3.Dot(u, cameraPosition);
            result.M43 = Vector3.Dot(f, cameraPosition);

            return result;
        }
    }
}
