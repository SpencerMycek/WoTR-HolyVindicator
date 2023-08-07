using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Utility;
using System;

namespace HolyVindicator.AdditionalSpellDescriptors
{
    [Flags]
    public enum ExtraSpellDescriptor : long
    {
        HolyVindicatorShield = 0x4000000000000000,
    }



    static class UIUtilityTexts_GetSpellDescriptor_Patch
    {
        static void Postfix(SpellDescriptor spellDescriptor, ref string __result)
        {
            if (spellDescriptor.Intersects((SpellDescriptor)ExtraSpellDescriptor.HolyVindicatorShield))
            {
                __result = maybeAddSeparator(__result) + "HolyVindicatorShield";
            }
        }


        static string maybeAddSeparator(string input)
        {
            if (input.Empty())
            {
                return input;
            }
            else
            {
                return input + ", ";
            }
        }
    }
}