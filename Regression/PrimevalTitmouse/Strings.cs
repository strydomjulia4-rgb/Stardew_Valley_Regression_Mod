using StardewValley;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PrimevalTitmouse
{
    //Lots of Regex functions to handle variability in our strings.
  /// <summary>
  /// String/template utility methods used by UI text and dialogue.
  /// </summary>
  public static class Strings
  {
    private static Data Data => Regression.t;
    private static Farmer Who => Game1.player;

    /// <summary>
    /// Builds a natural-language description of container condition (clean/wet/messy/drying).
    /// </summary>
    public static string DescribeUnderwear(Container u, string baseDescription = null)
    {
      string newValue = baseDescription != null ? baseDescription : u.description;
      float num1 = u.wetness / u.absorbency;
      float num2 = u.messiness / u.containment;
      if ((double) num1 == 0.0 && (double) num2 == 0.0)
      {
        newValue = !u.IsDrying() ? Strings.Data.Underwear_Clean.Replace("$UNDERWEAR_DESC$", newValue) : Strings.Data.Underwear_Drying.Replace("$UNDERWEAR_DESC$", newValue);
      }
      else
      {
        if ((double) num2 > 0.0)
        {
          for (int index = 0; index < Strings.Data.Underwear_Messy.Length; ++index)
          {
            float num3 = (float) (((double) index + 1.0) / ((double) Strings.Data.Underwear_Messy.Length - 1.0));
            if (index == Strings.Data.Underwear_Messy.Length - 1 || (double) num2 <= (double) num3)
            {
              newValue = Strings.ReplaceOptional(Strings.Data.Underwear_Messy[index].Replace("$UNDERWEAR_DESC$", newValue), (double) num1 > 0.0);
              break;
            }
          }
        }
        if ((double) num1 > 0.0)
        {
          for (int index = 0; index < Strings.Data.Underwear_Wet.Length; ++index)
          {
            float num3 = (float) (((double) index + 1.0) / ((double) Strings.Data.Underwear_Wet.Length - 1.0));
            if (index == Strings.Data.Underwear_Wet.Length - 1 || (double) num1 <= (double) num3)
            {
              string input = Strings.Data.Underwear_Wet[index].Replace("$UNDERWEAR_DESC$", newValue);
              Regex regex = new Regex("<([^>]*)>");
              newValue = (double) num2 != 0.0 ? regex.Replace(input, "$1") : regex.Replace(input, "");
              break;
            }
          }
        }
      }
      return u.GetPrefix() + " " + newValue;
        }


        /// <summary>
        /// Replaces all supported template variables in a localized message.
        /// </summary>
        public static string InsertVariables(string msg, Body b, Container c = null)
    {
      string str = msg;
      if (b != null)
        c = b.underwear;
      if (c != null)
        str = Strings.ReplaceOr(str.Replace("$UNDERWEAR_NAME$", c.name).Replace("$UNDERWEAR_PREFIX$", c.GetPrefix()).Replace("$UNDERWEAR_DESC$", c.description).Replace("$INSPECT_UNDERWEAR_NAME$", Strings.DescribeUnderwear(c, c.name)).Replace("$INSPECT_UNDERWEAR_DESC$", Strings.DescribeUnderwear(c, c.description)), !c.plural, "#");
      if (b != null)
      {
        b.SyncPantsFromOutfit();
        bool pantsIsSkirt = !b.pants.plural;
        string garmentRef = GetGarmentReference(b.pants.name, pantsIsSkirt);
        bool garmentLifted = IsLiftedGarment(garmentRef);
        string lowerAction = garmentLifted ? $"lift your {garmentRef}" : $"pull your {garmentRef} down";
        string lowerActionKnees = garmentLifted ? $"lift your {garmentRef}" : $"slide your {garmentRef} to your knees";
        str = str.Replace("$PANTS_NAME$", b.pants.name)
                 .Replace("$PANTS_PREFIX$", b.pants.GetPrefix())
                 .Replace("$PANTS_DESC$", b.pants.description)
                 .Replace("$PANTS_REFERENCE$", garmentRef)
                 .Replace("$PANTS_LOWER_ACTION$", lowerAction)
                 .Replace("$PANTS_LOWER_ACTION_KNEES$", lowerActionKnees)
                 .Replace("$BEDDING_DRYTIME$", Game1.getTimeOfDayString(b.bed.timeWhenDoneDrying.time));
      }
      return Strings.ReplaceOr(str, Strings.Who.IsMale, "/").Replace("$FARMERNAME$", Strings.Who.Name);
    }

    /// <summary>
    /// Simple direct variable replacement helper.
    /// </summary>
    public static string InsertVariable(string inputString, string variableName, string variableValue)
    {
        string outputString = inputString;
            outputString = outputString.Replace(variableName, variableValue);
        return outputString;
    }

    /// <summary>
    /// Chooses one random string from a message array.
    /// </summary>
    public static string RandString(string[] msgs = null)
    {
      return msgs[Regression.rnd.Next(msgs.Length)];
    }

    /// <summary>
    /// Expands an optional two-part token (e.g. &lt;a&b&gt;) into first/second/both/none.
    /// </summary>
    public static string ReplaceAndOr(string str, bool first, bool second, string splitChar = "&")
    {
      Regex regex = new Regex("<([^>" + splitChar + "]*)" + splitChar + "([^>]*)>");
      if (first && !second)
        return regex.Replace(str, "$1");
      if (!first & second)
        return regex.Replace(str, "$2");
      if (first & second)
        return regex.Replace(str, "$1 and $2");
      return regex.Replace(str, "");
    }

    /// <summary>
    /// Expands optional token wrappers &lt;...&gt; when keep=true.
    /// </summary>
    public static string ReplaceOptional(string str, bool keep)
    {
      return new Regex("<([^>]*)>").Replace(str, keep ? "$1" : "");
    }

    /// <summary>
    /// Expands a binary token (e.g. &lt;he/she&gt;) based on boolean choice.
    /// </summary>
    public static string ReplaceOr(string str, bool first, string splitChar = "/")
    {
      return new Regex("<([^>" + splitChar + "]*)" + splitChar + "([^>]*)>").Replace(str, first ? "$1" : "$2");
    }

    /// <summary>
    /// Returns wearable underwear keys from data (excluding helper templates).
    /// </summary>
    public static List<string> ValidUnderwearTypes()
    {
      List<string> list = Regression.t.Underwear_Options.Keys.ToList<string>();
      list.Remove("blue jeans");
      list.Remove("bed");
      return list;
    }

    private static string GetGarmentReference(string displayName, bool isSkirt)
    {
      if (string.IsNullOrWhiteSpace(displayName))
        return isSkirt ? "skirt" : "pants";

      string normalized = displayName.ToLowerInvariant();
      if (normalized.Contains("skirt"))
        return "skirt";
      if (normalized.Contains("kilt"))
        return "kilt";
      if (normalized.Contains("dress"))
        return "dress";
      if (normalized.Contains("short"))
        return "shorts";
      if (normalized.Contains("legging"))
        return "leggings";
      if (normalized.Contains("jean"))
        return "jeans";
      if (normalized.Contains("trouser") || normalized.Contains("slack"))
        return "pants";

      // Fallback wording for unknown/custom bottoms.
      return isSkirt ? "skirt" : "pants";
    }

    private static bool IsLiftedGarment(string garmentReference)
    {
      return garmentReference == "skirt" || garmentReference == "kilt" || garmentReference == "dress";
    }
  }
}
