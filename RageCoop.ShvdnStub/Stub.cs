// Server-side stand-in for the native ScriptHookVDotNet assembly.
// See RageCoop.ShvdnStub.csproj for why this exists.
//
// Every type here mirrors the name and kind (class / struct / enum / interface,
// and enum underlying type) of a type that libs/ScriptHookVDotNet3.dll 3.6.0.0
// references from ScriptHookVDotNet 3.6.0.0. Members are intentionally left
// out: the server must never call into them.

using System;
using System.Reflection;

[assembly: AssemblyMetadata("RageCoop.ShvdnStub", "true")]

namespace SHVDN
{
    public struct FVector3
    {
        public float X;
        public float Y;
        public float Z;
    }

    public struct ScrWeaponHudStats { }

    public interface IScriptTask { }

    public static class MemDataMarshal { }
    public static class MemScanner { }
    public static class NativeFunc { }
    public static class StringMarshal { }

    public static class NativeMemory
    {
        public struct EntityDamageRecordForReturnValue { }
        public struct ItemInfo { }
        public struct RageAtArrayPtr { }

        [Flags] public enum VehicleFlag1 : ulong { }
        [Flags] public enum VehicleFlag2 : ulong { }

        public static class PathFind { }
        public static class Ped { }
        public static class Vehicle { }
    }

    public sealed class Script { }
    public sealed class ScriptDomain : MarshalByRefObject { }
}
