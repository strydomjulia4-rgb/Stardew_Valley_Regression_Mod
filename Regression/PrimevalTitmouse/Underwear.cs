using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Custom inventory item wrapper for Regression underwear objects.
    /// Persists extra state (wet/messy/drying) that vanilla objects do not track.
    /// </summary>
    public class Underwear : StardewValley.Object
    {
        public static Color color;
        public Container container;
        public string id;

        public Underwear()
        {
            //base.Actor();
        }

        /// <summary>
        /// Ensures <see cref="container"/> exists; repairs items deserialized without custom state.
        /// </summary>
        public bool EnsureInitialized()
        {
            if (container != null)
                return true;

            if (TryRebuildFromModData())
                return true;

            // Fall back to a safe default type. Important: do NOT reference Name/DisplayName/Status here,
            // because those call EnsureInitialized(), causing recursion/stack overflow.
            string type = id;

            // If an item somehow kept partial modData but lost the flag, try reading it anyway.
            if (string.IsNullOrWhiteSpace(type)
                && modData.TryGetValue(UnderwearSerialization.ModDataPrefix + "type", out string modType)
                && !string.IsNullOrWhiteSpace(modType))
            {
                type = modType;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                // Prefer the default underwear the Body constructor uses, if present in the data file.
                if (Regression.t?.Underwear_Options != null && Regression.t.Underwear_Options.ContainsKey("dinosaur undies"))
                    type = "dinosaur undies";
                else
                    type = Regression.t?.Underwear_Options?.Keys?.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(type))
                return false;

            Initialize(type, wetness: 0.0f, messiness: 0.0f, count: Math.Max(1, Stack));
            return true;
        }

        private bool TryRebuildFromModData()
        {
            if (modData.TryGetValue(UnderwearSerialization.ModDataFlag, out string flag) != true || flag != "true")
                return false;

            var data = new Dictionary<string, string>();
            foreach (string key in new[] { "type", "wetness", "messiness", "stack", "dryingTime" })
            {
                if (modData.TryGetValue(UnderwearSerialization.ModDataPrefix + key, out string value))
                    data[key] = value;
            }

            if (!data.ContainsKey("type"))
                return false;

            rebuild(data, this);
            return container != null;
        }

        /// <summary>
        /// Creates a custom underwear item with persisted condition values.
        /// </summary>
        public Underwear(string type, float wetness = 0.0f, float messiness = 0.0f, int count = 1)
        {
            //base.Actor();
            this.Initialize(type, wetness, messiness, count);
        }

        /// <summary>
        /// Regression underwear cannot be dropped to prevent losing stateful items.
        /// </summary>
        public override bool canBeDropped()
        {
            return false;
        }

        /// <summary>
        /// Regression underwear cannot be gifted.
        /// </summary>
        public override bool canBeGivenAsGift()
        {
            return false;
        }

        /// <summary>
        /// Draws the custom underwear sprite in inventory menus.
        /// </summary>
        public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
        {
            if (!EnsureInitialized())
                return;

            Texture2D sprites = Animations.GetSprites();
            if (sprites == null)
            {
                base.drawInMenu(spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color, drawShadow);
                return;
            }

            int ratio = Animations.LARGE_SPRITE_DIM / Animations.SMALL_SPRITE_DIM;
            Vector2 offset = new(Game1.tileSize/2, Game1.tileSize/2); //Center of tile
            Vector2 origin = new(Animations.LARGE_SPRITE_DIM/2, Animations.LARGE_SPRITE_DIM/2); //Center of Sprite
            Rectangle source = Animations.UnderwearRectangle(container, FullnessType.None, Animations.LARGE_SPRITE_DIM);
            spriteBatch.Draw(sprites, location + offset, new Rectangle?(source), Color.White * transparency, 0.0f, origin, Game1.pixelZoom * scaleSize/ratio, SpriteEffects.None, layerDepth);
            if (drawStackNumber.Equals(StackDrawType.Hide) || maximumStackSize() <= 1 || (scaleSize <= 0.3 || Stack == int.MaxValue) || Stack <= 1)
                return;
            Utility.drawTinyDigits(Stack, spriteBatch, location + new Vector2(Game1.tileSize - Utility.getWidthOfTinyDigitString(Stack, 3f * scaleSize) + 3f * scaleSize, (float)(Game1.tileSize - 18.0 * scaleSize + 2.0)), 3f * scaleSize, 1f, Color.White);
        }

        /// <summary>
        /// Draws the held underwear sprite in-world.
        /// </summary>
        public override void drawWhenHeld(SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
        {
            if (!EnsureInitialized())
                return;

            Texture2D sprites = Animations.GetSprites();
            if (sprites == null)
                return;

            Rectangle rectangle = Animations.UnderwearRectangle(this.container, FullnessType.None, Animations.LARGE_SPRITE_DIM);
            spriteBatch.Draw(sprites, objectPosition, new Rectangle?(rectangle), Color.White, 0.0f, Vector2.Zero, Game1.pixelZoom/(Animations.LARGE_SPRITE_DIM/Animations.SMALL_SPRITE_DIM), SpriteEffects.None, Math.Max(0.0f, (f.StandingPixel.Y + 2) / 10000f));
        }

        /// <summary>
        /// Exports extra state needed to reconstruct this custom item after save/load.
        /// </summary>
        public Dictionary<string, string> getAdditionalSaveData()
        {
            if (!EnsureInitialized())
                throw new InvalidOperationException("Cannot serialize underwear without initialized container state.");
            return new Dictionary<string, string>()
            {
                {
                  "type",
                  container.name
                },
                {
                  "wetness",
                  string.Format("{0}",  container.wetness)
                },
                {
                  "messiness",
                  string.Format("{0}",  container.messiness)
                },
                {
                  "stack",
                  string.Format("{0}",  Stack)
                },
                {
                  "dryingTime",
                  container.serializeDryingDate()
                }
            };
        }

        /// <summary>
        /// Returns dynamic description text based on current condition.
        /// </summary>
        public override string getDescription()
        {
            if (!EnsureInitialized())
                return "";
            string source = Strings.DescribeUnderwear(this.container, (string)null);
            return Game1.parseText(source.First().ToString().ToUpper() + source.Substring(1), Game1.smallFont, Game1.tileSize * 6 + Game1.tileSize / 6);
        }

        /// <summary>
        /// Produces a cloned instance for split-stack/duplication behaviors.
        /// </summary>
        protected override Item GetOneNew()
        {
            if (!EnsureInitialized())
                return ItemRegistry.Create("(O)685", 1);
            return new Underwear(container.name, container.wetness, container.messiness, 1);
        }

        /// <summary>
        /// Converts this custom item into a vanilla placeholder item for serialization.
        /// </summary>
        public StardewValley.Object getReplacement()
        {
            var replacement = (StardewValley.Object)ItemRegistry.Create("(O)685", Stack);
            UnderwearSerialization.EmbedModData(replacement, getAdditionalSaveData());
            return replacement;
        }

        /// <summary>
        /// Initializes this item using a data-defined underwear type.
        /// </summary>
        public void Initialize(string type, float wetness, float messiness, int count = 1)
        {
            this.container = new Container(type);
            this.container.wetness = wetness;
            this.container.messiness = messiness;
            if (count > 1)
                Stack = count;
            id = type;
            name = container.name;
            Price = this.container.price;
        }

        /// <summary>
        /// Prevents stacking for non-clean or drying items.
        /// </summary>
        public override int maximumStackSize()
        {
            if (!EnsureInitialized())
                return 1;
            if (container.messiness > 0.0 || container.wetness > 0.0 || container.IsDrying())
                return 1;
            return base.maximumStackSize();
        }

        /// <summary>
        /// Rebuilds this custom item from serialized replacement metadata.
        /// </summary>
        public void rebuild(Dictionary<string, string> data, object replacement)
        {
            Initialize(
                data["type"],
                float.Parse(data["wetness"], CultureInfo.InvariantCulture),
                float.Parse(data["messiness"], CultureInfo.InvariantCulture),
                int.Parse(data["stack"], CultureInfo.InvariantCulture)
            );
            if (data.ContainsKey("dryingTime"))
            {
                this.container.parseDryingDate(data["dryingTime"]);
            }
        }

        /// <summary>
        /// Displays condition-prefixed item title in inventory UI.
        /// </summary>
        public override string DisplayName
        {
            get
            {
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Status + id);
            }
        }

        /// <summary>
        /// Name alias used by base game item APIs.
        /// </summary>
        public override string Name
        {
            get
            {
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Status + id);
            }
        }

        /// <summary>
        /// Prefix text representing current wet/messy/drying status.
        /// </summary>
        public string Status
        {
            get
            {
                if (!EnsureInitialized())
                    return "";
                if (container.messiness > 0.0 && container.wetness > 0.0)
                    return "wet and messy ";
                if (container.messiness > 0.0)
                    return "messy ";
                if (container.wetness > 0.0)
                    return "wet ";
                return container.IsDrying() ? "drying " : "";
            }
        }

        /// <summary>
        /// Helper mapping for legacy clothing grammar metadata.
        /// </summary>
        public static bool getPantsPlural(int itemNum)
        {
            //This was built based on the game's ClothingInformation.json file
            switch(itemNum)
            {
                case  -1: { return true; }
                case   0: { return true; }
                case   2: { return false; }
                case   3: { return false; }
                case   4: { return false; }
                case   5: { return true; }
                case   6: { return false; }
                case   7: { return false; }
                case   8: { return true; }
                case   9: { return true; }
                case  10: { return true; }
                case  11: { return false; }
                case  12: { return true; }
                case  13: { return true; }
                case  14: { return true; }
                case  15: { return true; }
                case 998: { return true; }
                case 999: { return true; }
            }
            return false;
        }
    }
}