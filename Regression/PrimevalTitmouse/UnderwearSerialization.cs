using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;
using System.Collections.Generic;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Swaps <see cref="Underwear"/> for vanilla placeholders during XML serialization (save + multiplayer)
    /// and restores them afterward from embedded modData or sidecar JSON snapshots.
    /// </summary>
    public static class UnderwearSerialization
    {
        public const string ModDataFlag = "Regression.IsUnderwear";
        public const string ModDataPrefix = "Regression.";

        /// <summary>
        /// Replaces underwear slots with serializable vanilla items and returns restore metadata.
        /// </summary>
        public static Dictionary<int, Dictionary<string, string>> ReplaceForXml(Inventory items)
        {
            var replacements = new Dictionary<int, Dictionary<string, string>>();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is Underwear underwear)
                {
                    replacements[i] = underwear.getAdditionalSaveData();
                    items[i] = underwear.getReplacement();
                }
            }

            return replacements;
        }

        /// <summary>
        /// Restores underwear from a replace-for-xml snapshot (used after WriteXml).
        /// </summary>
        public static void RestoreFromSnapshot(Inventory items, Dictionary<int, Dictionary<string, string>> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
                return;

            foreach (KeyValuePair<int, Dictionary<string, string>> entry in snapshot)
            {
                var underwear = new Underwear();
                underwear.rebuild(entry.Value, items[entry.Key]);
                items[entry.Key] = underwear;
            }
        }

        /// <summary>
        /// Restores underwear from modData on placeholder items (used after ReadXml / load).
        /// </summary>
        public static void RestoreFromModData(Inventory items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item?.modData.TryGetValue(ModDataFlag, out string flag) != true || flag != "true")
                    continue;

                var data = new Dictionary<string, string>();
                foreach (string key in new[] { "type", "wetness", "messiness", "stack", "dryingTime" })
                {
                    if (item.modData.TryGetValue(ModDataPrefix + key, out string value))
                        data[key] = value;
                }

                if (!data.ContainsKey("type"))
                    continue;

                var underwear = new Underwear();
                underwear.rebuild(data, item);
                items[i] = underwear;
            }
        }

        /// <summary>
        /// Stores underwear state on a vanilla placeholder so it survives XML serialization.
        /// </summary>
        public static void EmbedModData(Item replacement, Dictionary<string, string> saveData)
        {
            replacement.modData[ModDataFlag] = "true";
            foreach (KeyValuePair<string, string> entry in saveData)
                replacement.modData[ModDataPrefix + entry.Key] = entry.Value;
        }

        /// <summary>
        /// Rebuilds placeholder underwear and repairs uninitialized custom instances in an inventory.
        /// </summary>
        public static void ValidateInventory(Inventory items)
        {
            if (items == null)
                return;

            RestoreFromModData(items);

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is Underwear underwear)
                    underwear.EnsureInitialized();
            }
        }

        /// <summary>
        /// Repairs underwear stored in every chest on the current farm.
        /// </summary>
        public static void ValidateWorldChests()
        {
            foreach (var location in Game1.locations)
            {
                foreach (var obj in location.Objects.Values)
                {
                    if (obj is Chest chest)
                        ValidateInventory(chest.Items);
                }
            }
        }
    }
}
