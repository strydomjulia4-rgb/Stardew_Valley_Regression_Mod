using StardewValley;
using System.Collections.Generic;

namespace Regression
{
    /// <summary>
    /// Debug utility that fast-forwards the in-game clock with temporary NPC speedup.
    /// Triggered by debug hotkey in the main mod.
    /// </summary>
    internal static class TimeMagic
    {
        /// <summary>
        /// Speeds up villagers and fast-forwards several ten-minute ticks.
        /// </summary>
        public static void doMagic()
        {
            Game1.player.forceTimePass = true;
            Game1.playSound("stardrop");

            for (int i = 0; i < Game1.locations.Count; i++)
            {
                GameLocation cLocation = Game1.locations[i];
                for (int j = 0; j < cLocation.characters.Count; j++)
                {
                    NPC cNPC = cLocation.characters[j];
                    if (cNPC.IsVillager)
                        ((Character)cNPC).addedSpeed = 10;
                }
            }

            for (int index = 0; index < 12; ++index)
            {
                // ISSUE: method pointer
                DelayedAction delayedAction = new DelayedAction((index + 1) * 1000 / 2, moveTimeForward);
                ((List<DelayedAction>)Game1.delayedActions).Add(delayedAction);
            }
            // ISSUE: method pointer
            DelayedAction delayedAction1 = new DelayedAction(7000, slowDown);
            ((List<DelayedAction>)Game1.delayedActions).Add(delayedAction1);
        }

        /// <summary>
        /// Advances in-game clock by ten minutes and plays SFX.
        /// </summary>
        private static void moveTimeForward()
        {
            Game1.playSound("parry");
            Game1.performTenMinuteClockUpdate();
        }

        /// <summary>
        /// Restores villager speed after debug time acceleration completes.
        /// </summary>
        private static void slowDown()
        {
            for (int i = 0; i < Game1.locations.Count; i++)
            {
                GameLocation cLocation = Game1.locations[i];
                for (int j = 0; j < cLocation.characters.Count; j++)
                {
                    NPC cNPC = cLocation.characters[j];
                    if (cNPC.IsVillager)
                        ((Character)cNPC).addedSpeed = 0;
                }
            }
        }
    }
}
