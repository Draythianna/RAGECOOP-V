using GTA;
using GTA.Math;
using RageCoop.Core;
using SHVDN;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RageCoop.Client
{
    internal unsafe class MemPatch
    {
        private readonly byte[] _data;
        private readonly byte[] _orginal;
        private readonly IntPtr _address;
        public MemPatch(byte* address, byte[] data)
        {
            _data = data;
            _orginal = new byte[data.Length];
            _address = (IntPtr)address;
            Marshal.Copy((IntPtr)address, _orginal, 0, data.Length);
        }
        public void Install()
        {
            Marshal.Copy(_data, 0, _address, _data.Length);
        }
        public void Uninstall()
        {
            Marshal.Copy(_orginal, 0, _address, _orginal.Length);
        }
    }

    internal static unsafe class Memory
    {
        public static MemPatch VignettingPatch;
        public static MemPatch VignettingCallPatch;
        public static MemPatch TimeScalePatch;
        static Memory()
        {
            // Weapon/radio wheel slow-mo patch
            // Thanks @CamxxCore, https://github.com/CamxxCore/GTAVWeaponWheelMod
            var resultPtr = Game.FindPattern("\x38\x51\x64\x74\x19", "xxxxx");
            // Skip the patch if the pattern isn't found (other game builds): a throwing static
            // constructor breaks every Memory call, including the position reads.
            if (resultPtr == IntPtr.Zero) { return; }
            byte* result = (byte*)resultPtr.ToPointer();
            var address = result + 26;
            address = address + *(int*)address + 4u;
            VignettingPatch = new MemPatch(address, new byte[] { RET, 0x90, 0x90, 0x90, 0x90 });
            VignettingCallPatch = new MemPatch(result + 8, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });
            TimeScalePatch = new MemPatch(result + 34, new byte[] { XOR_32_64, 0xD2 });

        }
        public static void ApplyPatches()
        {
            VignettingPatch?.Install();
            VignettingCallPatch?.Install();
            TimeScalePatch?.Install();
        }
        public static void RestorePatches()
        {
            VignettingPatch?.Uninstall();
            VignettingCallPatch?.Uninstall();
            TimeScalePatch?.Uninstall();
        }
        #region PATCHES
        #endregion
        #region OFFSET-CONST
        public const int PositionOffset = 144;
        public const int VelocityOffset = 800;
        public const int MatrixOffset = 96;
        #endregion
        #region OPCODE
        private const byte XOR_32_64 = 0x31;
        private const byte RET = 0xC3;
        #endregion
        // Reading through a deleted entity's memory address crashes the game, so these fall back to
        // the regular natives when the entity is gone or has no address.
        public static Vector3 ReadPosition(this Entity e)
        {
            if (e == null || !e.Exists()) return Vector3.Zero;
            // Read through the native rather than a fixed memory offset, which can differ between game builds.
            return e.Position;
        }
        public static Quaternion ReadQuaternion(this Entity e)
        {
            if (e == null || !e.Exists()) return Quaternion.Identity;
            return Quaternion.RotationMatrix(e.Matrix);
        }
        public static Vector3 ReadRotation(this Entity e) => e.ReadQuaternion().ToEulerDegrees();
        public static Vector3 ReadVelocity(this Ped e)
        {
            if (e == null || !e.Exists()) return Vector3.Zero;
            return e.Velocity;
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