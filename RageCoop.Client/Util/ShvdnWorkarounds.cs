using GTA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RageCoop.Client
{
    /// <summary>
    /// Workarounds for bugs in specific ScriptHookVDotNet builds, plus one-time error logging.
    /// </summary>
    internal static class ShvdnWorkarounds
    {
        private static bool _projectilesChecked;
        private static readonly HashSet<string> _logged = new HashSet<string>();

        /// <summary>
        /// Log an error once per key. Uses the Error level so it still shows when a server
        /// lowers the client's log level.
        /// </summary>
        internal static void LogOnce(string key, string message)
        {
            lock (_logged)
            {
                if (_logged.Count > 200 || !_logged.Add(key)) return;
            }
            Main.Logger?.Error(message);
        }

        /// <summary>
        /// ScriptHookVDotNet Enhanced 3.9.0.6 reads the projectile pool wrongly on Legacy GTA, so
        /// World.GetAllProjectiles() throws every time. Server resources that replace the sync loop
        /// call it too, and then stop before any player is created (invisible players). If it
        /// throws, clear ScriptHookVDotNet's projectile pool address so it returns an empty list.
        /// Outgoing projectile sync is lost on that ScriptHookVDotNet build either way.
        /// </summary>
        internal static unsafe void CheckProjectilePool()
        {
            if (_projectilesChecked) return;
            _projectilesChecked = true;
            try
            {
                World.GetAllProjectiles();
                return;
            }
            catch (Exception ex)
            {
                LogOnce("projectiles", "World.GetAllProjectiles failed: " + ex.Message);
            }
            try
            {
                var nativeMemory = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("SHVDN.NativeMemory", false))
                    .FirstOrDefault(t => t != null);
                var field = nativeMemory?.GetField("s_projectilePoolAddress", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null)
                {
                    LogOnce("projectiles-fix", "Could not find ScriptHookVDotNet's projectile pool field; the projectile list stays broken.");
                    return;
                }
                field.SetValue(null, field.FieldType.IsPointer ? Pointer.Box(null, field.FieldType) : Activator.CreateInstance(field.FieldType));
                World.GetAllProjectiles();
                LogOnce("projectiles-fix", "Disabled ScriptHookVDotNet's broken projectile pool; World.GetAllProjectiles now returns an empty list.");
            }
            catch (Exception ex)
            {
                LogOnce("projectiles-fix", "Projectile pool workaround failed: " + ex);
            }
        }
    }
}
