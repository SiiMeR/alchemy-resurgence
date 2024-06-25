using Vintagestory.API.Common;

namespace Alchemy;

public static class Util
{
     public static int GetStrengthModifier(ItemStack content)
     {
          var strength = content.Item.Variant["strength"];

          return strength switch
          {
               "strong" => 4,
               "medium" => 2,
               _ => 1
          };
     }

}