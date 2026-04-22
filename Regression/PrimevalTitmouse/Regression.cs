using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Regression;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using xTile.Dimensions;

namespace PrimevalTitmouse
{
    /// <summary>
    /// SMAPI entry point for the Regression gameplay module.
    /// It wires game/input/UI events and routes them into the persistent <see cref="Body"/> state.
    /// </summary>
    public class Regression : Mod
    {
        public const string ToiletTextureAssetKey = "Regression_Toilets";
        private static readonly (string Id, string DisplayName, int SpriteIndex)[] ToiletVariants = new[]
        {
            // Each color variant occupies a 3x4 block; use the full 1x2 toilet sprite.
            ("Regression_Toilet_White", "White Toilet", 0),
            ("Regression_Toilet_Gray", "Gray Toilet", 12),
            ("Regression_Toilet_Purple", "Purple Toilet", 24),
            ("Regression_Toilet_Mint", "Mint Toilet", 36),
            ("Regression_Toilet_Yellow", "Yellow Toilet", 48),
            ("Regression_Toilet_Pink", "Pink Toilet", 60)
        };
        public static int lastTimeOfDay = 0;
        public static bool morningHandled = true;
        public static Random rnd = new Random();
        public static bool started = false;
        public Body body;
        public static Config config;
        public static IModHelper help;
        public static IMonitor monitor;
        public bool shiftHeld;
        public static Data t;
        public static Farmer who;
        private float tickCD1 = 0;
        private float tickCD2 = 0;

        const float timeInTick = (1f/43f); //One second realtime ~= 1/43 hours in game
        /// <summary>
        /// Initializes config/data and subscribes all SMAPI event handlers.
        /// </summary>
        public override void Entry(IModHelper h)
        {
            help = h;
            monitor = Monitor;
            StatusBars.Initialize(h, Monitor);
            config = Helper.ReadConfig<Config>();
            t = Helper.Data.ReadJsonFile<Data>(string.Format("{0}.json", (object)config.Lang)) ?? Helper.Data.ReadJsonFile<Data>("en.json");
            h.Events.GameLoop.Saving += new EventHandler<SavingEventArgs>(this.BeforeSave);
            h.Events.GameLoop.GameLaunched += new EventHandler<GameLaunchedEventArgs>(this.OnGameLaunched);
            h.Events.GameLoop.DayStarted += new EventHandler<DayStartedEventArgs>(ReceiveAfterDayStarted);
            h.Events.GameLoop.OneSecondUpdateTicking += new EventHandler<OneSecondUpdateTickingEventArgs>(ReceiveUpdateTick);
            h.Events.GameLoop.TimeChanged += new EventHandler<TimeChangedEventArgs>(ReceiveTimeOfDayChanged);
            h.Events.Content.AssetRequested += this.OnAssetRequested;
            h.Events.Input.ButtonPressed += new EventHandler<ButtonPressedEventArgs>(ReceiveKeyPress);
            h.Events.Input.ButtonPressed += new EventHandler<ButtonPressedEventArgs>(ReceiveMouseChanged);
            h.Events.Display.MenuChanged += new EventHandler<MenuChangedEventArgs>(ReceiveMenuChanged);
            h.Events.Display.RenderingHud += new EventHandler<RenderingHudEventArgs>(ReceivePreRenderHudEvent);

            // Fallback keybind configuration path that works even when GMCM API signatures differ.
            h.ConsoleCommands.Add("reg_listkeys", "List current Regression keybinds.", this.CommandListKeys);
            h.ConsoleCommands.Add("reg_setkey", "Set a Regression keybind: reg_setkey <action> <SButton>.", this.CommandSetKey);
            h.ConsoleCommands.Add("reg_savekeys", "Save current Regression config to disk.", this.CommandSaveKeys);
            h.ConsoleCommands.Add("reg_resetkeys", "Reset Regression keybinds to defaults.", this.CommandResetKeys);
        }

        /// <summary>
        /// Draws hunger/thirst and continence HUD bars (plus underwear icon when enabled).
        /// </summary>
        public void DrawStatusBars()
        {
            // Position custom bars near the vanilla HUD meters.
            int x1 = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right - (65 + (int)((StatusBars.barWidth)));
            // Bottom-align with vanilla stamina/health meters (more padding than the old custom bars needed).
            int bottomPad = Game1.pixelZoom * 4;
            int y1 = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Bottom - (bottomPad + (int)((StatusBars.barHeight)));

            if (Game1.currentLocation is MineShaft || Game1.currentLocation is Woods || Game1.currentLocation is SlimeHutch || Game1.currentLocation is VolcanoDungeon || who.health < who.maxHealth)
                x1 -= 58;

            if (!config.NoHungerAndThirst || PrimevalTitmouse.Regression.config.Debug)
            {
                float percentage1 = body.GetHungerPercent();
                // StatusBars.png: left=Thirst, right=Hunger
                StatusBars.DrawStatusBar(x1, y1, percentage1, false, true, new Color(115, byte.MaxValue, 56));
                int x2 = x1 - (4 + StatusBars.barWidth);
                float percentage2 = body.GetThirstPercent();
                StatusBars.DrawStatusBar(x2, y1, percentage2, false, false, new Color(117, 225, byte.MaxValue));
                x1 = x2 - (4 + StatusBars.barWidth);
            }
            if (config.Debug)
            {
                if (config.Messing)
                {
                    float percentage = body.GetBowelPercent();
                    // Debug.png: left=Bladder, right=Bowels
                    StatusBars.DrawStatusBar(x1, y1, percentage, true, true, new Color(146, 111, 91));
                    x1 -= 4 + StatusBars.barWidth;
                }
                if (config.Wetting)
                {
                    float percentage = body.GetBladderPercent();
                    StatusBars.DrawStatusBar(x1, y1, percentage, true, false, new Color(byte.MaxValue, 225, 56));
                }
            }
            if (!config.Wetting && !config.Messing)
                return;
            int y2 = (Game1.player.questLog).Count == 0 ? 250 : 310;
            Animations.DrawUnderwearIcon(body.underwear, Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right - 94, y2);
        }

        /// <summary>
        /// Debug helper that opens an item menu containing all underwear variants and sample resources.
        /// </summary>
        private void GiveUnderwear()
        {
            List<Item> objList = new List<Item>();
            foreach (string validUnderwearType in Strings.ValidUnderwearTypes())
                objList.Add(new Underwear(validUnderwearType, 0.0f, 0.0f, 20));
            objList.Add(ItemRegistry.Create("(O)399", 99));
            objList.Add(ItemRegistry.Create("(O)348", 99));
            Game1.activeClickableMenu = new ItemGrabMenu(objList);
        }

        /// <summary>
        /// Reconstructs serialized custom underwear items from replacement slots.
        /// </summary>
        private static void restoreItems(StardewValley.Inventories.Inventory items, Dictionary<int, Dictionary<string, string>> invReplacement)
        {
            foreach (KeyValuePair<int, Dictionary<string, string>> entry in invReplacement)
            {
                var underwear = new Underwear();
                underwear.rebuild(entry.Value, items[entry.Key]);
                items[entry.Key] = underwear;
            }
        }

        /// <summary>
        /// Restores mod state at the start of each day and triggers morning messaging.
        /// </summary>
        private void ReceiveAfterDayStarted(object sender, DayStartedEventArgs e)
        {
            // Restore our serialized body state for this save.
            body = Helper.Data.ReadJsonFile<Body>(string.Format("{0}/RegressionSave.json", Constants.SaveFolderName)) ?? new Body();
            started = true;
            who = Game1.player;

            var invReplacement = Helper.Data.ReadJsonFile< Dictionary<int, Dictionary<string, string>>>(string.Format("{0}/RegressionSaveInv.json", Constants.SaveFolderName));
            if (invReplacement != null)
            {
                restoreItems(Game1.player.Items, invReplacement);
            }

            var chestReplacement = Helper.Data.ReadJsonFile<Dictionary<string, Dictionary<int, Dictionary<string, string>>>>(string.Format("{0}/RegressionSaveChest.json", Constants.SaveFolderName));
            if (chestReplacement != null)
            {
                int locId = 0;
                foreach (var location in Game1.locations)
                {
                    foreach (var obj in location.Objects.Values)
                    {
                        var id = string.Format("{0}-{1}-{2}", locId, obj.TileLocation.X, obj.TileLocation.Y);
                        if (obj is Chest chest && chestReplacement.ContainsKey(id))
                        {
                            restoreItems(chest.Items, chestReplacement[id]);
                        }
                    }
                    locId++;
                }
            }

            Animations.AnimateNight(body);
            HandleMorning(sender, e);
        }

        /// <summary>
        /// Runs morning body-state calculations after load.
        /// </summary>
        private void HandleMorning(object Sender, DayStartedEventArgs e)
        {
            body.HandleMorning();
        }

        /// <summary>
        /// Replaces custom underwear items with vanilla placeholders before save.
        /// </summary>
        private static Dictionary<int, Dictionary<string, string>> replaceItems(StardewValley.Inventories.Inventory items)
        {
            var replacements = new Dictionary<int, Dictionary<string, string>>();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is Underwear)
                {
                    var underwear = (items[i] as Underwear);
                    items[i] = underwear.getReplacement();
                    replacements.Add(i, underwear.getAdditionalSaveData());
                }
            }

            return replacements;
        }

        //Save Mod related variables in separate JSON. Also trigger night handling if not on the very first day.
        private void BeforeSave(object Sender, SavingEventArgs e)
        {
            body.bedtime = lastTimeOfDay;
            if (Game1.dayOfMonth != 1 || Game1.currentSeason != "spring" || Game1.year != 1)
                body.HandleNight();
            if (string.IsNullOrWhiteSpace(Constants.SaveFolderName))
                return;

            var chestReplacements = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>();

            int locId = 0;
            foreach (var location in Game1.locations)
            {
                foreach (var obj in location.Objects.Values)
                {
                    if (obj is Chest chest)
                    {
                        var id = string.Format("{0}-{1}-{2}", locId, obj.TileLocation.X, obj.TileLocation.Y);
                        chestReplacements.Add(id, replaceItems(chest.Items));
                    }
                }

                foreach (var furn in location.furniture.OfType<StorageFurniture>())
                {
                    Monitor.Log(string.Format("Found storage furniture {0}", furn.DisplayName), LogLevel.Info);
                }
                locId++;
            }

            var invReplacements = replaceItems(Game1.player.Items);

            Helper.Data.WriteJsonFile(string.Format("{0}/RegressionSave.json", Constants.SaveFolderName), body);
            Helper.Data.WriteJsonFile(string.Format("{0}/RegressionSaveInv.json", Constants.SaveFolderName), invReplacements);
            Helper.Data.WriteJsonFile(string.Format("{0}/RegressionSaveChest.json", Constants.SaveFolderName), chestReplacements);
        }

        /// <summary>
        /// Per-second simulation update: time-based body decay plus consume detection.
        /// </summary>
        private void ReceiveUpdateTick(object sender, OneSecondUpdateTickingEventArgs e)
        {

                //Ignore everything until we've started the day
                if (!started)
                    return;


                //If time is moving, update our body state (Hunger, thirst, etc.)
                if (ShouldTimePass())
                {
                    this.body.HandleTime(timeInTick);
                    if (tickCD1 != 0) //Former Bug: When consuming items, they would give 2-3 goes at the "Handle eating and drinking." if statement below. Cause: This function would trigger multiple times while the if statement below was still true, causing it to fire multiple times.
                    {
                        tickCD2 += 1; //This should trigger multiple times during the eating animation.
                    }
                    if (tickCD2 >= 2) //Setting this to 2 should be able to balance preventing double triggers and ensuring no loss in intentional triggers.
                    {
                        tickCD1 = 0;
                        tickCD2 = 0;
                    }
                    
                }

                //Handle eating and drinking.
                if (Game1.player.isEating && Game1.activeClickableMenu == null && tickCD1 == 0)
                {
                    // itemToEat can be null for a short window during animation transitions.
                    if (who.itemToEat != null)
                        body.Consume(who.itemToEat.Name);
                    tickCD1 += 1;
                }
        }

        //Determine if we need to handle time passing (not the same as Game time passing)
        private static bool ShouldTimePass()
        {
            return ((Game1.game1.IsActive || Game1.options.pauseWhenOutOfFocus == false) && (Game1.paused == false && Game1.dialogueUp == false) && (Game1.currentMinigame == null && Game1.eventUp == false && Game1.activeClickableMenu == null) && Game1.fadeToBlack == false);
        }

        //Interprete key-presses
        /// <summary>
        /// Handles hotkeys for debug tools and manual wet/mess test actions.
        /// </summary>
        private void ReceiveKeyPress(object sender, ButtonPressedEventArgs e)
        {
            //If we haven't started the day, ignore the key presses
            if (!started)
                return;

            // Handle debug keybinds (defaults use LeftAlt+... chords).
            if (config.Debug)
            {
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugDecreaseEverything)
                {
                    body.DecreaseEverything();
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugIncreaseEverything)
                {
                    body.IncreaseEverything();
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugGiveUnderwear)
                {
                    GiveUnderwear();
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugTimeMagic)
                {
                    TimeMagic.doMagic();
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugToggleWetting)
                {
                    config.Wetting = !config.Wetting;
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugToggleMessing)
                {
                    config.Messing = !config.Messing;
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugToggleEasyMode)
                {
                    config.Easymode = !config.Easymode;
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugWorseContinence)
                {
                    if (e.IsDown(SButton.LeftShift))
                        body.ChangeBowelContinence(0.1f);
                    else
                        body.ChangeBladderContinence(0.1f);
                    return;
                }
                if (e.IsDown(SButton.LeftAlt) && e.Button == config.KeyDebugBetterContinence)
                {
                    if (e.IsDown(SButton.LeftShift))
                        body.ChangeBowelContinence(-0.1f);
                    else
                        body.ChangeBladderContinence(-0.1f);
                    return;
                }
            }

            if (e.Button == config.KeyWet)
            {
                body.Wet(true, !e.IsDown(SButton.LeftShift));
                return;
            }
            if (e.Button == config.KeyMess)
            {
                body.Mess(true, !e.IsDown(SButton.LeftShift));
                return;
            }
            if (e.Button == config.KeyCheckUnderwear)
            {
                Animations.CheckUnderwear(body);
                return;
            }
            if (e.Button == config.KeyCheckPants) /*F4 is reserved for screenshot mode*/
            {
                Animations.CheckPants(body);
                return;
            }
            if (e.Button == config.KeyToggleDebug)
            {
                config.Debug = !config.Debug;
                return;
            }
        }

        /// <summary>
        /// Registers settings (including keybinds) with Generic Mod Config Menu when available.
        /// </summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            object api = this.Helper.ModRegistry.GetApi("spacechase0.GenericModConfigMenu");
            if (api == null)
            {
                this.Monitor.Log("GMCM is not installed or API is unavailable.", LogLevel.Trace);
                return;
            }

            bool registered = TryInvokeMethod(api, "Register", this.ModManifest, (Action)(() => config = new Config()), (Action)(() => this.Helper.WriteConfig(config)), false)
                || TryInvokeMethod(api, "RegisterModConfig", this.ModManifest, (Action)(() => config = new Config()), (Action)(() => this.Helper.WriteConfig(config)));
            if (!registered)
            {
                this.Monitor.Log("GMCM API found, but Register failed due to an unexpected signature.", LogLevel.Warn);
                return;
            }

            string[] allButtons = Enum.GetNames(typeof(SButton));
            TryInvokeMethod(api, "AddSectionTitle", this.ModManifest, (Func<string>)(() => "Controls"), null);
            AddKeybindOption(api, "Wet", () => config.KeyWet, value => config.KeyWet = value, allButtons);
            AddKeybindOption(api, "Mess", () => config.KeyMess, value => config.KeyMess = value, allButtons);
            AddKeybindOption(api, "Check Underwear", () => config.KeyCheckUnderwear, value => config.KeyCheckUnderwear = value, allButtons);
            AddKeybindOption(api, "Check Pants", () => config.KeyCheckPants, value => config.KeyCheckPants = value, allButtons);
            AddKeybindOption(api, "Toggle Debug", () => config.KeyToggleDebug, value => config.KeyToggleDebug = value, allButtons);

            TryInvokeMethod(api, "AddSectionTitle", this.ModManifest, (Func<string>)(() => "Debug Controls:"), null);
            AddKeybindOption(api, "Debug: Decrease (Alt + key)", () => config.KeyDebugDecreaseEverything, value => config.KeyDebugDecreaseEverything = value, allButtons);
            AddKeybindOption(api, "Debug: Increase (Alt + key)", () => config.KeyDebugIncreaseEverything, value => config.KeyDebugIncreaseEverything = value, allButtons);
            AddKeybindOption(api, "Debug: Underwear (Alt + key)", () => config.KeyDebugGiveUnderwear, value => config.KeyDebugGiveUnderwear = value, allButtons);
            AddKeybindOption(api, "Debug: Time Magic (Alt + key)", () => config.KeyDebugTimeMagic, value => config.KeyDebugTimeMagic = value, allButtons);
            AddKeybindOption(api, "Debug: Toggle Wet (Alt + key)", () => config.KeyDebugToggleWetting, value => config.KeyDebugToggleWetting = value, allButtons);
            AddKeybindOption(api, "Debug: Toggle Mess (Alt + key)", () => config.KeyDebugToggleMessing, value => config.KeyDebugToggleMessing = value, allButtons);
            AddKeybindOption(api, "Debug: Toggle Easy (Alt + key)", () => config.KeyDebugToggleEasyMode, value => config.KeyDebugToggleEasyMode = value, allButtons);
            AddKeybindOption(api, "Debug: Worse Cont. (Alt + key)", () => config.KeyDebugWorseContinence, value => config.KeyDebugWorseContinence = value, allButtons);
            AddKeybindOption(api, "Debug: Better Cont. (Alt + key)", () => config.KeyDebugBetterContinence, value => config.KeyDebugBetterContinence = value, allButtons);
        }

        /// <summary>
        /// Adds a keybind option using whichever GMCM method exists in the installed version.
        /// </summary>
        private void AddKeybindOption(object api, string label, Func<SButton> getter, Action<SButton> setter, string[] allowedValues)
        {
            bool added = TryInvokeMethod(
                    api,
                    "AddKeybind",
                    this.ModManifest,
                    getter,
                    setter,
                    (Func<string>)(() => label),
                    (Func<string>)(() => "Press any key/button to bind. Debug actions still require LeftAlt.")
                )
                || TryInvokeMethod(
                    api,
                    "RegisterSimpleOption",
                    this.ModManifest,
                    label,
                    "Press any key/button to bind. Debug actions still require LeftAlt.",
                    getter,
                    setter
                )
                || TryInvokeMethod(
                    api,
                    "AddTextOption",
                    this.ModManifest,
                    (Func<string>)(() => getter().ToString()),
                    (Action<string>)(value =>
                    {
                        if (Enum.TryParse(value, true, out SButton parsed))
                            setter(parsed);
                    }),
                    (Func<string>)(() => label),
                    (Func<string>)(() => "Choose a key (F1-F10 are supported). Debug actions still require LeftAlt."),
                    allowedValues,
                    null
                )
                || TryInvokeMethod(
                    api,
                    "AddTextOption",
                    this.ModManifest,
                    (Func<string>)(() => getter().ToString()),
                    (Action<string>)(value =>
                    {
                        if (Enum.TryParse(value, true, out SButton parsed))
                            setter(parsed);
                    }),
                    (Func<string>)(() => label),
                    (Func<string>)(() => "Choose a key (F1-F10 are supported). Debug actions still require LeftAlt.")
                );

            if (!added)
                this.Monitor.Log($"GMCM keybind option '{label}' could not be registered with this API version.", LogLevel.Warn);
        }

        private static bool TryInvokeMethod(object target, string methodName, params object[] providedArgs)
        {
            foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name == methodName))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (providedArgs.Length > parameters.Length)
                    continue;

                object[] finalArgs = new object[parameters.Length];
                bool compatible = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i < providedArgs.Length)
                    {
                        object arg = providedArgs[i];
                        if (!IsParameterCompatible(parameters[i].ParameterType, arg))
                        {
                            compatible = false;
                            break;
                        }
                        finalArgs[i] = arg;
                    }
                    else if (parameters[i].IsOptional)
                    {
                        finalArgs[i] = parameters[i].DefaultValue;
                    }
                    else
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                    continue;

                method.Invoke(target, finalArgs);
                return true;
            }

            return false;
        }

        private static bool IsParameterCompatible(Type parameterType, object arg)
        {
            if (arg == null)
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;

            Type argType = arg.GetType();
            return parameterType.IsAssignableFrom(argType);
        }

        private void CommandListKeys(string command, string[] args)
        {
            this.Monitor.Log("Regression keybinds:", LogLevel.Info);
            foreach (string line in GetKeybindLines())
                this.Monitor.Log($"  {line}", LogLevel.Info);
            this.Monitor.Log("Use: reg_setkey <action> <SButton> (example: reg_setkey wet K)", LogLevel.Info);
        }

        private void CommandSetKey(string command, string[] args)
        {
            if (args.Length < 2)
            {
                this.Monitor.Log("Usage: reg_setkey <action> <SButton>", LogLevel.Info);
                this.Monitor.Log("Run reg_listkeys to see valid actions.", LogLevel.Info);
                return;
            }

            string action = args[0];
            string buttonText = args[1];
            if (!Enum.TryParse(buttonText, true, out SButton parsedButton))
            {
                this.Monitor.Log($"Invalid SButton '{buttonText}'. Example values: K, F1, MouseLeft, LeftShift.", LogLevel.Warn);
                return;
            }

            if (!TrySetKeybind(action, parsedButton, out string canonicalAction))
            {
                this.Monitor.Log($"Unknown action '{action}'. Run reg_listkeys for valid actions.", LogLevel.Warn);
                return;
            }

            this.Helper.WriteConfig(config);
            this.Monitor.Log($"Set '{canonicalAction}' to '{parsedButton}' and saved config.", LogLevel.Info);
        }

        private void CommandSaveKeys(string command, string[] args)
        {
            this.Helper.WriteConfig(config);
            this.Monitor.Log("Regression keybind config saved.", LogLevel.Info);
        }

        private void CommandResetKeys(string command, string[] args)
        {
            Config defaults = new Config();
            config.KeyWet = defaults.KeyWet;
            config.KeyMess = defaults.KeyMess;
            config.KeyCheckUnderwear = defaults.KeyCheckUnderwear;
            config.KeyCheckPants = defaults.KeyCheckPants;
            config.KeyToggleDebug = defaults.KeyToggleDebug;
            config.KeyDebugDecreaseEverything = defaults.KeyDebugDecreaseEverything;
            config.KeyDebugIncreaseEverything = defaults.KeyDebugIncreaseEverything;
            config.KeyDebugGiveUnderwear = defaults.KeyDebugGiveUnderwear;
            config.KeyDebugTimeMagic = defaults.KeyDebugTimeMagic;
            config.KeyDebugToggleWetting = defaults.KeyDebugToggleWetting;
            config.KeyDebugToggleMessing = defaults.KeyDebugToggleMessing;
            config.KeyDebugToggleEasyMode = defaults.KeyDebugToggleEasyMode;
            config.KeyDebugWorseContinence = defaults.KeyDebugWorseContinence;
            config.KeyDebugBetterContinence = defaults.KeyDebugBetterContinence;
            this.Helper.WriteConfig(config);

            this.Monitor.Log("Regression keybinds reset to defaults and saved.", LogLevel.Info);
        }

        private IEnumerable<string> GetKeybindLines()
        {
            yield return $"wet={config.KeyWet}";
            yield return $"mess={config.KeyMess}";
            yield return $"checkunderwear={config.KeyCheckUnderwear}";
            yield return $"checkpants={config.KeyCheckPants}";
            yield return $"toggledebug={config.KeyToggleDebug}";
            yield return $"debugdecrease={config.KeyDebugDecreaseEverything}";
            yield return $"debugincrease={config.KeyDebugIncreaseEverything}";
            yield return $"debugunderwear={config.KeyDebugGiveUnderwear}";
            yield return $"debugtimemagic={config.KeyDebugTimeMagic}";
            yield return $"debugtogglewetting={config.KeyDebugToggleWetting}";
            yield return $"debugtogglemessing={config.KeyDebugToggleMessing}";
            yield return $"debugtoggleeasymode={config.KeyDebugToggleEasyMode}";
            yield return $"debugworsecontinence={config.KeyDebugWorseContinence}";
            yield return $"debugbettercontinence={config.KeyDebugBetterContinence}";
        }

        private static string NormalizeActionName(string actionName)
        {
            return new string(actionName
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();
        }

        private static bool TrySetKeybind(string actionName, SButton button, out string canonicalAction)
        {
            string key = NormalizeActionName(actionName);
            canonicalAction = null;

            switch (key)
            {
                case "wet":
                    config.KeyWet = button;
                    canonicalAction = "wet";
                    return true;
                case "mess":
                    config.KeyMess = button;
                    canonicalAction = "mess";
                    return true;
                case "checkunderwear":
                    config.KeyCheckUnderwear = button;
                    canonicalAction = "checkunderwear";
                    return true;
                case "checkpants":
                    config.KeyCheckPants = button;
                    canonicalAction = "checkpants";
                    return true;
                case "toggledebug":
                    config.KeyToggleDebug = button;
                    canonicalAction = "toggledebug";
                    return true;
                case "debugdecrease":
                case "debugdecreaseeverything":
                    config.KeyDebugDecreaseEverything = button;
                    canonicalAction = "debugdecrease";
                    return true;
                case "debugincrease":
                case "debugincreaseeverything":
                    config.KeyDebugIncreaseEverything = button;
                    canonicalAction = "debugincrease";
                    return true;
                case "debugunderwear":
                case "debuggiveunderwear":
                    config.KeyDebugGiveUnderwear = button;
                    canonicalAction = "debugunderwear";
                    return true;
                case "debugtimemagic":
                    config.KeyDebugTimeMagic = button;
                    canonicalAction = "debugtimemagic";
                    return true;
                case "debugtogglewetting":
                    config.KeyDebugToggleWetting = button;
                    canonicalAction = "debugtogglewetting";
                    return true;
                case "debugtogglemessing":
                    config.KeyDebugToggleMessing = button;
                    canonicalAction = "debugtogglemessing";
                    return true;
                case "debugtoggleeasymode":
                    config.KeyDebugToggleEasyMode = button;
                    canonicalAction = "debugtoggleeasymode";
                    return true;
                case "debugworsecontinence":
                    config.KeyDebugWorseContinence = button;
                    canonicalAction = "debugworsecontinence";
                    return true;
                case "debugbettercontinence":
                    config.KeyDebugBetterContinence = button;
                    canonicalAction = "debugbettercontinence";
                    return true;
                default:
                    return false;
            }
        }

        //A menu has been opened, figure out if we need to modify it
        /// <summary>
        /// Hooks menu openings to inject sleep/mail/shop custom behavior.
        /// </summary>
        private void ReceiveMenuChanged(object sender, MenuChangedEventArgs e)
        {
            //Don't do anything if our day hasn't started
            if (!started)
                return;

            DialogueBox attemptToSleepMenu;
            ShopMenu currentShopMenu;

            //If we try to sleep, check if the bed is done drying (only matters in Hard Mode)
            if (Game1.currentLocation is FarmHouse && (attemptToSleepMenu = e.NewMenu as DialogueBox) != null && Game1.currentLocation.lastQuestionKey == "Sleep" && !config.Easymode)
            {
                //If enough time has passed, the bed has dried
                if (body.bed.IsDrying())
                {
                    Response[] sleepAttemptResponses = attemptToSleepMenu.responses;
                    if (sleepAttemptResponses.Length == 2)
                    {
                        Response response = sleepAttemptResponses[1];
                        Game1.currentLocation.answerDialogue(response);
                        Game1.currentLocation.lastQuestionKey = null;
                        attemptToSleepMenu.closeDialogue();
                        Animations.AnimateDryingBedding(body);
                    }
                }
            }
            //If we're in the mailbox, handle the initial letter from Jodi that contains protection
            else if (e.NewMenu is LetterViewerMenu && Game1.currentLocation is Farm)
            {
                LetterViewerMenu letterMenu = (LetterViewerMenu)e.NewMenu;
                Mail.ShowLetter(letterMenu);
            }
            //If we're trying to shop, handle the underwear inventory
            else if((currentShopMenu = e.NewMenu as ShopMenu) != null)
            {
                //Default to all underwear being available
                List<string> allUnderwear = Strings.ValidUnderwearTypes();
                List<string> availableUnderwear = new List<string>(allUnderwear);
                bool underwearAvailableAtShop = false;
                if (IsPierreShop(currentShopMenu))
                {
                    // Pierre's shop does not sell the Joja diaper.
                    availableUnderwear.Remove("Joja diaper");
                    underwearAvailableAtShop = true;
                    AddToiletsToShop(currentShopMenu);
                } else if(Game1.currentLocation is JojaMart)
                {
                    //Joja shop ONLY sels the Joja diaper and a cloth diaper
                    availableUnderwear.Clear();
                    availableUnderwear.Add("Joja diaper");
                    availableUnderwear.Add("Cloth diaper");
                    underwearAvailableAtShop = true;
                }

                if(underwearAvailableAtShop)
                {
                    foreach(string type in availableUnderwear)
                    {
                        Underwear underwear = new Underwear(type, 0.0f, 0.0f, 1);
                        currentShopMenu.forSale.Add(underwear);
                        currentShopMenu.itemPriceAndStock.Add(underwear, new ItemStockInformation(underwear.container.price, 999));
                    }
                }
            }
        }

        //Check if we are at a natural water source
        /// <summary>
        /// Returns true when tool target tile has natural water properties.
        /// </summary>
        private static bool AtWaterSource()
        {
            GameLocation currentLocation = Game1.currentLocation;
            Vector2 toolLocation = who.GetToolLocation(false);
            int x = (int)toolLocation.X;
            int y = (int)toolLocation.Y;
            return currentLocation.doesTileHaveProperty(x / Game1.tileSize, y / Game1.tileSize, "Water", "Back") != null || currentLocation.doesTileHaveProperty(x / Game1.tileSize, y / Game1.tileSize, "WaterSource", "Back") != null;
        }

        //Check if we are at the Well (and its constructed)
        /// <summary>
        /// Returns true when targeting a finished well building.
        /// </summary>
        private static bool AtWell()
        {
            GameLocation currentLocation = Game1.currentLocation;
            Vector2 toolLocation = who.GetToolLocation(false);
            int x = (int)toolLocation.X;
            int y = (int)toolLocation.Y;
            Vector2 vector2 = new Vector2((float)(x / Game1.tileSize), y / Game1.tileSize);
            return currentLocation.IsBuildableLocation() && currentLocation.getBuildingAt(vector2) != null && (currentLocation.getBuildingAt(vector2).buildingType.Value.Equals("Well") && currentLocation.getBuildingAt(vector2).daysOfConstructionLeft.Value <= 0);

        }

        //Handle Mouse Clicks/Movement
        /// <summary>
        /// Handles context-sensitive left-click actions (drink/swap/wash underwear).
        /// </summary>
        private void ReceiveMouseChanged(object sender, ButtonPressedEventArgs e)
        {
            //Ignore if we aren't started or otherwise paused
            if (!(Game1.game1.IsActive && !Game1.paused && started))
            {
                return;
            }

            //Handle a Left Click
            if (e.Button == SButton.MouseLeft)
            {
                //If Left click is already being interpreted by another event (or we otherwise wouldn't process such an event. Ignore it.
                if ((Game1.dialogueUp || Game1.currentMinigame != null || (Game1.eventUp || Game1.activeClickableMenu != null) || Game1.fadeToBlack) || (who.isRidingHorse() || !who.canMove || (Game1.player.isEating || who.canOnlyWalk) || who.FarmerSprite.pauseForSingleAnimation))
                    return;

                ////If we're holding the watering can, attempt to drink from it.
                /////This is the highest priority (apparently?)
                if (who.CurrentTool != null && who.CurrentTool is WateringCan && e.IsDown(SButton.LeftShift))
                {
                    this.body.DrinkWateringCan();
                    return;
                }

                //Otherwise Check if we're holding underwear
                Underwear activeObject = who.ActiveObject as Underwear;
                if (activeObject != null)
                {
                    //If the Underwear we are holding isn't currently wet, messy, or drying; change into it.
                    if ((double)activeObject.container.wetness + (double)activeObject.container.messiness == 0.0 && !activeObject.container.IsDrying())
                    {
                        who.reduceActiveItemByOne(); //Take it out of inventory
                        Container container = body.ChangeUnderwear(activeObject); //Put on the new underwear and return the old
                        Underwear underwear = new Underwear(container.name, container.wetness, container.messiness, 1);

                        //If the underwear returned is not removable, destroy it
                        if (!container.removable) { }
                        //Otherwise put the old underwear into the inventory, but pull up the management window if it can't fit
                        else if (!who.addItemToInventoryBool(underwear, false))
                        {
                            List<Item> objList = new List<Item>();
                            objList.Add(underwear);
                            Game1.activeClickableMenu = new ItemGrabMenu(objList);
                        }
                    }
                    //If it is wet, messy or drying, check if we can wash it
                    else if (activeObject.container.washable)
                    {
                        //Are we at a water source? If so, wash the underwear.
                        if (AtWaterSource())
                        {
                            activeObject.container.Wash();
                            Animations.AnimateWashingUnderwear(activeObject.container);
                        }
                    }
                    return; //Done with underwear
                }
                    
                    
                //If we're at a water source, and not holding underwear, drink from it.
                if ((AtWaterSource()|| AtWell()) && e.IsDown(SButton.LeftShift))
                  this.body.DrinkWaterSource();
            }
                
        }

        //If approppriate, draw bars for Hunger, thirst, bladder and bowels
        /// <summary>
        /// Draw callback for custom HUD bars during normal gameplay scenes.
        /// </summary>
        public void ReceivePreRenderHudEvent(object sender, RenderingHudEventArgs args)
        {
            if (!started || Game1.currentMinigame != null || Game1.eventUp || Game1.globalFade)
                return;
            DrawStatusBars();
        }

        /// <summary>
        /// Handles scheduled events tied to in-game clock changes.
        /// </summary>
        private void ReceiveTimeOfDayChanged(object sender, TimeChangedEventArgs e)
        {
            lastTimeOfDay = Game1.timeOfDay;

            //If its 6:10AM, handle delivering mail
            if (Game1.timeOfDay == 610)
                Mail.CheckMail();

            //If its earlier than 6:30, we aren't wet/messy don't notice that we're still soiled (or don't notice with ~5% chance even if soiled)
            if (rnd.NextDouble() >= 0.0555555559694767 || body.underwear.wetness + (double)body.underwear.messiness <= 0.0 || Game1.timeOfDay < 630)
                return;
            Animations.AnimateStillSoiled(this.body);
        }

        public Regression()
        {
            //base.Actor();
        }

        /// <summary>
        /// Injects custom toilet furniture data and its texture into game assets.
        /// </summary>
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;

                    foreach (var toiletVariant in ToiletVariants)
                    {
                        // Data/Furniture format:
                        // name/type/tilesheet size/bounding box/rotations/price/placement/display/sprite index/texture
                        // Draw as 1x2 (full toilet), but use 1x1 collision/seat on the bowl tile.
                        data[toiletVariant.Id] = $"{toiletVariant.Id}/chair/1 2/1 1/1/100/-1/{toiletVariant.DisplayName}/{toiletVariant.SpriteIndex}/{ToiletTextureAssetKey}";
                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(ToiletTextureAssetKey))
            {
                e.LoadFromModFile<Texture2D>("Assets/toilets.png", AssetLoadPriority.Exclusive);
            }
        }

        public static bool IsToiletFurnitureId(string id)
        {
            return ToiletVariants.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPierreShop(ShopMenu menu)
        {
            if (Game1.currentLocation is SeedShop)
                return true;

            if (menu == null)
                return false;

            object portraitPerson = menu.GetType().GetField("portraitPerson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu)
                ?? menu.GetType().GetProperty("portraitPerson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu);
            string portraitName = portraitPerson?.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(portraitPerson) as string;
            if (!string.IsNullOrWhiteSpace(portraitName) && portraitName.Equals("Pierre", StringComparison.OrdinalIgnoreCase))
                return true;

            string shopId = menu.GetType().GetField("storeContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu) as string
                ?? menu.GetType().GetProperty("storeContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu) as string
                ?? menu.GetType().GetField("storeOwner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu) as string
                ?? menu.GetType().GetProperty("storeOwner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu) as string;
            if (!string.IsNullOrWhiteSpace(shopId) && (shopId.IndexOf("Pierre", StringComparison.OrdinalIgnoreCase) >= 0 || shopId.IndexOf("SeedShop", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            return false;
        }

        private void AddToiletsToShop(ShopMenu shopMenu)
        {
            foreach (var toiletVariant in ToiletVariants)
            {
                try
                {
                    string qualifiedId = $"(F){toiletVariant.Id}";
                    bool alreadyListed = shopMenu.forSale.Any(item => item != null && item.QualifiedItemId == qualifiedId);
                    if (alreadyListed)
                        continue;

                    Item toilet = ItemRegistry.Create(qualifiedId);
                    if (toilet == null)
                    {
                        Monitor.Log($"Toilet item '{qualifiedId}' could not be created (null result).", LogLevel.Warn);
                        continue;
                    }

                    shopMenu.forSale.Add(toilet);
                    shopMenu.itemPriceAndStock[toilet] = new ItemStockInformation(100, 99);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed adding toilet '{toiletVariant.Id}' to Pierre: {ex.Message}", LogLevel.Warn);
                }
            }
        }
    }
}
