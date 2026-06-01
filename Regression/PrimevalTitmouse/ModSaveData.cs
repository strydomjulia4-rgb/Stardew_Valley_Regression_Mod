using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Save path helpers so each farmer has their own mod data on shared farms.
    /// </summary>
    public static class ModSaveData
    {
        private const string BodyFile = "RegressionSave.json";
        private const string InventoryFile = "RegressionSaveInv.json";
        public const string ChestFile = "RegressionSaveChest.json";

        /// <summary>
        /// Per-local-farmer data (body, inventory sidecar).
        /// </summary>
        public static string PlayerPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(Constants.SaveFolderName))
                return fileName;

            Farmer player = Game1.player;
            if (player?.UniqueMultiplayerID is long id && id != 0)
                return $"{Constants.SaveFolderName}/{id}_{fileName}";

            return $"{Constants.SaveFolderName}/{fileName}";
        }

        /// <summary>
        /// Shared farm/world data (chest sidecar).
        /// </summary>
        public static string WorldPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(Constants.SaveFolderName))
                return fileName;
            return $"{Constants.SaveFolderName}/{fileName}";
        }

        public static string BodyPath => PlayerPath(BodyFile);

        public static string InventoryPath => PlayerPath(InventoryFile);

        public static string ChestPath => WorldPath(ChestFile);

        /// <summary>
        /// Reads body state for the local farmer, with legacy single-player fallback.
        /// </summary>
        public static Body ReadBody(IModHelper helper)
        {
            Body data = helper.Data.ReadJsonFile<Body>(BodyPath);
            if (data != null)
                return data;
            return helper.Data.ReadJsonFile<Body>(WorldPath(BodyFile));
        }

        /// <summary>
        /// Reads inventory underwear sidecar for the local farmer, with legacy fallback.
        /// </summary>
        public static Dictionary<int, Dictionary<string, string>> ReadInventorySidecar(IModHelper helper)
        {
            var data = helper.Data.ReadJsonFile<Dictionary<int, Dictionary<string, string>>>(InventoryPath);
            if (data != null)
                return data;
            return helper.Data.ReadJsonFile<Dictionary<int, Dictionary<string, string>>>(WorldPath(InventoryFile));
        }
    }
}
