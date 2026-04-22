namespace PrimevalTitmouse
{
    /// <summary>
    /// User-editable config values loaded from `config.json`.
    /// </summary>
    public class Config
    {
        public bool AlwaysNoticeAccidents;
        public bool Debug;
        public bool Easymode;
        public string Lang;
        public bool Messing;
        public bool NoFriendshipPenalty;
        public bool NoHungerAndThirst;
        public bool Wetting;
        public int BladderLossContinenceRate;
        public int BowelLossContinenceRate;
        public int BladderGainContinenceRate;
        public int BowelGainContinenceRate;
        public StardewModdingAPI.SButton KeyWet = StardewModdingAPI.SButton.F1;
        public StardewModdingAPI.SButton KeyMess = StardewModdingAPI.SButton.F2;
        public StardewModdingAPI.SButton KeyCheckUnderwear = StardewModdingAPI.SButton.F5;
        public StardewModdingAPI.SButton KeyCheckPants = StardewModdingAPI.SButton.F6;
        public StardewModdingAPI.SButton KeyToggleDebug = StardewModdingAPI.SButton.F9;
        public StardewModdingAPI.SButton KeyDebugDecreaseEverything = StardewModdingAPI.SButton.F1;
        public StardewModdingAPI.SButton KeyDebugIncreaseEverything = StardewModdingAPI.SButton.F2;
        public StardewModdingAPI.SButton KeyDebugGiveUnderwear = StardewModdingAPI.SButton.F3;
        public StardewModdingAPI.SButton KeyDebugTimeMagic = StardewModdingAPI.SButton.F5;
        public StardewModdingAPI.SButton KeyDebugToggleWetting = StardewModdingAPI.SButton.F6;
        public StardewModdingAPI.SButton KeyDebugToggleMessing = StardewModdingAPI.SButton.F7;
        public StardewModdingAPI.SButton KeyDebugToggleEasyMode = StardewModdingAPI.SButton.F8;
        public StardewModdingAPI.SButton KeyDebugWorseContinence = StardewModdingAPI.SButton.S;
        public StardewModdingAPI.SButton KeyDebugBetterContinence = StardewModdingAPI.SButton.W;
    }
}
