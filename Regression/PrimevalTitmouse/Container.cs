using System;
using System.Collections.Generic;
using StardewValley;
using static StardewValley.Minigames.MineCart.Whale;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Represents anything that can hold wet/mess values (underwear, pants, bedding).
    /// Handles absorbency/containment, washing, and drying timers.
    /// </summary>
    public class Container
    {
        public float absorbency;
        public float containment;
        public string description;
        public float messiness;
        public string name;
        public bool plural;
        public int price;
        public int spriteIndex;
        public bool washable;
        public float wetness;
        public int dryingTime;
        public bool removable;
        public int durability;

        public struct Date
        {
            public int time;
            public int day;
            public int season;
            public int year;
        }

        public Date timeWhenDoneDrying;
        private bool drying = false;

        //This class describes anything that we could wet/mess in. Usually underwear, but it could also be something like the bed.
        //These functions are pretty self-explanatory
        /// <summary>
        /// Creates an empty container state.
        /// </summary>
        public Container()
        {
            wetness = 0.0f;
            messiness = 0.0f;
            drying = false;
        }
        /// <summary>
        /// Creates a container from a configured underwear template key.
        /// </summary>
        public Container(string type)
        {
            Container c;

            if (!Regression.t.Underwear_Options.TryGetValue(type, out c))
                throw new Exception(string.Format("Invalid underwear choice: {0}", type));

            Initialize(c, c.wetness, c.messiness, c.durability);
        }

        /// <summary>
        /// Copy constructor preserving template metadata and current state.
        /// </summary>
        public Container(Container c)
        {
            Initialize(c, c.wetness, c.messiness, c.durability);
        }


        /// <summary>
        /// Creates a container from template key with explicit condition values.
        /// </summary>
        public Container(string type, float wetness, float messiness, int durability)
        {
            this.wetness = 0.0f;
            this.messiness = 0.0f;
            drying = false;
            Initialize(type, wetness, messiness, durability);
        }

        /// <summary>
        /// Returns grammar prefix for singular/plural item naming.
        /// </summary>
        public string GetPrefix()
        {
            if (plural) return "a pair of";
            return "a";
        }

        /// <summary>
        /// Starts drying cycle and decrements durability when washable.
        /// </summary>
        public void Wash()
        {
            if (washable)
            {
                if(durability != -1 && durability != 0) //infinite durability if -1
                {
                    durability--;
                }
                drying = true;
                timeWhenDoneDrying.time = Game1.timeOfDay + dryingTime;
                timeWhenDoneDrying.day = Game1.dayOfMonth;
                timeWhenDoneDrying.season = Utility.getSeasonNumber(Game1.currentSeason);
                timeWhenDoneDrying.year = Game1.year;

                if(timeWhenDoneDrying.time >= 2400)
                {
                    timeWhenDoneDrying.time -= 2400;
                    timeWhenDoneDrying.day += 1;
                }
                if(timeWhenDoneDrying.day > 28)
                {
                    timeWhenDoneDrying.day -= 28;
                    timeWhenDoneDrying.season += 1;
                }
                if(timeWhenDoneDrying.season > 4)
                {
                    timeWhenDoneDrying.season -= 4;
                    timeWhenDoneDrying.year += 1;
                }
            }
        }

        /// <summary>
        /// Indicates item has reached zero durability and should be removed.
        /// </summary>
        public bool MarkedForDestroy()
        {
            return (durability == 0)&&washable;
        }

        /// <summary>
        /// Returns true while drying timer is still active; auto-cleans when complete.
        /// </summary>
        public bool IsDrying()
        {
            if (!drying) return false;
            Date currentDate;
            currentDate.time = Game1.timeOfDay;
            currentDate.day = Game1.dayOfMonth;
            currentDate.season = Utility.getSeasonNumber(Game1.currentSeason);
            currentDate.year = Game1.year;

            bool yearEq   = currentDate.year == timeWhenDoneDrying.year;
            bool seasonEq = currentDate.season == timeWhenDoneDrying.season;
            bool dayEq    = currentDate.day == timeWhenDoneDrying.day;
            bool timeEq   = currentDate.time == timeWhenDoneDrying.time;
            bool yearGt   = currentDate.year > timeWhenDoneDrying.year;
            bool seasonGt = currentDate.season > timeWhenDoneDrying.season;
            bool dayGt    = currentDate.day > timeWhenDoneDrying.day;
            bool timeGt   = currentDate.time > timeWhenDoneDrying.time;
            if ((yearGt) || (yearEq && seasonGt) || (yearEq && seasonEq && dayGt) || (yearEq && seasonEq && dayEq && (timeGt || timeEq)))
            {
                drying = false;
                wetness = 0;
                messiness = 0;
            }
            return drying;
        }

        /// <summary>
        /// Adds wetness and returns overflow amount beyond absorbency.
        /// </summary>
        public float AddPee(float amount)
        {
            wetness += amount;
            float difference = wetness - absorbency;
            if (difference > 0)
            {
                wetness = absorbency;
                return difference;
            }
            return 0.0f;
        }

        /// <summary>
        /// Adds messiness and returns overflow amount beyond containment.
        /// </summary>
        public float AddPoop(float amount)
        {
            messiness += amount;
            float difference = messiness - containment;
            if (difference > 0)
            {
                messiness = containment;
                return difference;
            }
            return 0.0f;
        }

        /// <summary>
        /// Internal initializer from another container template plus explicit state.
        /// </summary>
        private void Initialize(Container c, float wetness, float messiness, int durability)
        {
            name = c.name;
            description = c.description;
            absorbency = c.absorbency;
            containment = c.containment;
            spriteIndex = c.spriteIndex;
            price = c.price;
            washable = c.washable;
            plural = c.plural;
            dryingTime = c.dryingTime;
            drying = c.drying;
            timeWhenDoneDrying = c.timeWhenDoneDrying;
            removable = c.removable;
            this.wetness = wetness;
            this.messiness = messiness;
            this.durability = durability;
        }

        /// <summary>
        /// Public initializer from template key plus explicit state.
        /// </summary>
        public void Initialize(string type, float wetness, float messiness, int durability)
        {
            Container c;

            if (!Regression.t.Underwear_Options.TryGetValue(type, out c))
                throw new Exception(string.Format("Invalid underwear choice: {0}", type));

            Initialize(c, wetness, messiness, durability);
        }
        /// <summary>
        /// Restores drying timestamp from serialized save text.
        /// </summary>
        public void parseDryingDate(string date)
        {
            if (date == "")
            {
                drying = false;
                return;
            }

            string[] splitted = date.Split("-");
            if (splitted.Length != 4)
            {
                drying = false;
                return;
            }
            drying = true;
            if (!int.TryParse(splitted[0], out timeWhenDoneDrying.time)
                || !int.TryParse(splitted[1], out timeWhenDoneDrying.day)
                || !int.TryParse(splitted[2], out timeWhenDoneDrying.season)
                || !int.TryParse(splitted[3], out timeWhenDoneDrying.year))
            {
                drying = false;
            }
        }
        /// <summary>
        /// Serializes active drying timestamp for save metadata.
        /// </summary>
        public string serializeDryingDate()
        {
            if (!drying)
            {
                return "";
            }
            return string.Format("{0}-{1}-{2}-{3}", timeWhenDoneDrying.time, timeWhenDoneDrying.day, timeWhenDoneDrying.season, timeWhenDoneDrying.year);
        }
    }
}
