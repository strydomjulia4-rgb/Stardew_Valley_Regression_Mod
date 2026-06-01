using HarmonyLib;
using StardewValley.Inventories;
using System.Collections.Generic;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Ensures custom underwear items can pass through XML inventory serialization used by saves and multiplayer.
    /// </summary>
    [HarmonyPatch]
    internal static class SerializationPatches
    {
        [HarmonyPatch(typeof(Inventory), "WriteXml")]
        private static class InventoryWriteXmlPatch
        {
            public static void Prefix(Inventory __instance, ref Dictionary<int, Dictionary<string, string>> __state)
            {
                __state = UnderwearSerialization.ReplaceForXml(__instance);
            }

            public static void Postfix(Inventory __instance, Dictionary<int, Dictionary<string, string>> __state)
            {
                UnderwearSerialization.RestoreFromSnapshot(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(Inventory), "ReadXml")]
        private static class InventoryReadXmlPatch
        {
            public static void Postfix(Inventory __instance)
            {
                UnderwearSerialization.RestoreFromModData(__instance);
            }
        }

        /// <summary>
        /// Applies inventory serialization patches at mod startup.
        /// </summary>
        internal static void Apply(Harmony harmony)
        {
            harmony.PatchAll(typeof(SerializationPatches).Assembly);
        }
    }
}
