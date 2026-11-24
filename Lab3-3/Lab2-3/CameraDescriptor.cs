using Silk.NET.Maths;
using System;

namespace Szeminarium
{
    internal class CameraDescriptor
    {
        // Kamera pozicioja a vilagban
        public Vector3D<float> Position { get; private set; } = new Vector3D<float>(0, 0, 5);

        // Vizszintes szog (radianban)
        public float Yaw { get; private set; } = 0f;

        // Fuggoleges billentes (radianban)
        public float Pitch { get; private set; } = 0f;

        public CameraDescriptor()
        {
        }

        public CameraDescriptor(Vector3D<float> initialPosition)
        {
            Position = initialPosition;

            // Kamera iranyitasa a kocka kozeppontja fele indulasnal
            Vector3D<float> dir = Vector3D<float>.Zero - initialPosition;
            float xzLen = MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
            if (xzLen > 0.0001f)
            {
                Yaw = MathF.Atan2(-dir.X, -dir.Z);
                Pitch = MathF.Atan2(dir.Y, xzLen);
            }
        }

        // Elore mutato egysegvektor a szogekbol
        public Vector3D<float> Forward
        {
            get
            {
                float x = MathF.Cos(Pitch) * -MathF.Sin(Yaw);
                float y = MathF.Sin(Pitch);
                float z = MathF.Cos(Pitch) * -MathF.Cos(Yaw);
                return Vector3D.Normalize(new Vector3D<float>(x, y, z));
            }
        }

        // Felfele mutato vektor
        public Vector3D<float> UpVector
        {
            get
            {
                return new Vector3D<float>(0, 1, 0);
            }
        }

        // Celpont a nezet matrixhoz
        public Vector3D<float> Target
        {
            get
            {
                return Position + Forward;
            }
        }

        // Folyamatos mozgas az X-Z sikon (Doom stilus)
        public void MoveForward(float distance)
        {
            Vector3D<float> moveDir = new Vector3D<float>(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
            Position += moveDir * distance;
        }

        public void MoveBackward(float distance)
        {
            Vector3D<float> moveDir = new Vector3D<float>(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
            Position -= moveDir * distance;
        }

        public void StrafeLeft(float distance)
        {
            Vector3D<float> rightDir = new Vector3D<float>(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));
            Position -= rightDir * distance;
        }

        public void StrafeRight(float distance)
        {
            Vector3D<float> rightDir = new Vector3D<float>(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));
            Position += rightDir * distance;
        }

        public void StrafeUp(float distance)
        {
            Position += new Vector3D<float>(0, 1, 0) * distance;
        }

        public void StrafeDown(float distance)
        {
            Position -= new Vector3D<float>(0, 1, 0) * distance;
        }

        // Egeres nezelodes szogmodositoi
        public void AddYaw(float angle)
        {
            Yaw += angle;
        }

        public void AddPitch(float angle)
        {
            Pitch = Math.Clamp(Pitch + angle, -1.5f, 1.5f);
        }
    }
}