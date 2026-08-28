using GTA;
using GTA.Math;
using SHVDN;
using System;
using System.Collections.Generic;

namespace RageCoop.Client
{
    internal static unsafe class Memory
    {
        #region OFFSET-CONST
        public const int PositionOffset = 144;
        public const int VelocityOffset = 800;
        public const int MatrixOffset = 96;
        #endregion

        public static void ApplyPatches()
        {
            // Selection wheel slow-mo patch handled by SHVDN Enhanced internally
            NativeMemory.InstallSelectionWheelsPatch();
        }
        public static void RestorePatches()
        {
            NativeMemory.UninstallSelectionWheelsPatch();
        }

        public static Vector3 ReadPosition(this Entity e) => ReadVector3(e.MemoryAddress + PositionOffset);
        public static Quaternion ReadQuaternion(this Entity e) => Quaternion.RotationMatrix(e.Matrix);
        public static Vector3 ReadRotation(this Entity e) => ToEulerDegrees(e.ReadQuaternion());
        public static Vector3 ReadVelocity(this Ped e) => ReadVector3(e.MemoryAddress + VelocityOffset);

        public static Vector3 ToEulerDegrees(Quaternion q)
        {
            // Roll (X)
            float sinrCosp = 2f * (q.W * q.X + q.Y * q.Z);
            float cosrCosp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
            float roll = (float)Math.Atan2(sinrCosp, cosrCosp);

            // Pitch (Y)
            float sinp = 2f * (q.W * q.Y - q.Z * q.X);
            float pitch = Math.Abs(sinp) >= 1f
                ? (sinp < 0f ? -1f : 1f) * ((float)Math.PI / 2f)
                : (float)Math.Asin(sinp);

            // Yaw (Z)
            float sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
            float cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
            float yaw = (float)Math.Atan2(sinyCosp, cosyCosp);

            return new Vector3(
                roll  * (180f / (float)Math.PI),
                pitch * (180f / (float)Math.PI),
                yaw   * (180f / (float)Math.PI)
            );
        }

        public static Vector3 ReadVector3(IntPtr address)
        {
            float* ptr = (float*)address.ToPointer();
            return new Vector3()
            {
                X = *ptr,
                Y = ptr[1],
                Z = ptr[2]
            };
        }
        public static List<int> FindOffset(float toSearch, IntPtr start, int range = 1000, float tolerance = 0.01f)
        {
            var foundOffsets = new List<int>(100);
            for (int i = 0; i <= range; i++)
            {
                var val = *(float*)(start + i).ToPointer();
                if (Math.Abs(val - toSearch) < tolerance)
                {
                    foundOffsets.Add(i);
                }
            }
            return foundOffsets;
        }
    }
}