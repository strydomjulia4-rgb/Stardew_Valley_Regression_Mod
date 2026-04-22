using StardewModdingAPI.Events;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Manages Regression-specific mailbox content and starter item rewards.
    /// </summary>
    public static class Mail
    {
        private static bool letterShown = false;
        public static IModHelper helper;
        private static readonly string initialRegressionLetterTitle = "jodi_initial_regression";
        private static string letterContents = Regression.t.Jodi_Initial_Letter[0];
        private static List<Item> initialSupplies = new();

        /// <summary>
        /// Queues the mod starter letter and supplies on eligible saves.
        /// </summary>
        public static void CheckMail()
        {
            //Give an extra letter in the beginning to give some starting supplies
            if (!Game1.player.hasOrWillReceiveMail(initialRegressionLetterTitle))
            {
                initialSupplies = new();
                letterShown = false;

                //Always give turnips and diapers
                initialSupplies.Add(ItemRegistry.Create("(O)399", 20));           //OLD CODE: (new StardewValley.Object("399", 20, false, -1, 0)
                initialSupplies.Add(new Underwear("heart diaper", 0.0f, 0.0f, 40));

                //If we're in Hard mode, also give pull-up.
                if (!Regression.config.Easymode)
                {
                    initialSupplies.Add(new Underwear("lavender pullup", 0.0f, 0.0f, 15));
                }
                // Build this message fresh each time we queue the starter letter.
                letterContents = Regression.t.Jodi_Initial_Letter[0] + "[#]A Little... Protection.";
                Game1.mailbox.Add(initialRegressionLetterTitle);
                Dictionary<string, string> mails = Game1.content.Load<Dictionary<string, string>>("Data\\mail");
                if(!mails.ContainsKey(initialRegressionLetterTitle))
                  mails.Add(initialRegressionLetterTitle, letterContents);

                //Just to test we haven't broken other letters;
                //Game1.mailbox.Add("robinWell");
            }
        }

        //The typical, built-in "%item <id> <qty>%% doesn't work for 2 reasons
        //1) It only supports 1 item, were we want to give multiple
        //2) It only supports items with IDs, whereas our underwear is a non-ID'd object
        /// <summary>
        /// Grants starter items when the starter letter is opened.
        /// Falls back to ground drops when inventory is full.
        /// </summary>
        public static void ShowLetter(LetterViewerMenu letterViewer)
        {
            string mail = letterViewer.mailTitle;
            if (mail == initialRegressionLetterTitle && !letterShown)
            {
                letterShown = true;
                foreach (Item item in initialSupplies)
                {
                    if (!Game1.player.addItemToInventoryBool(item))
                        Game1.createItemDebris(item, Game1.player.getStandingPosition(), -1);
                }
            }
        }
    }
}
