using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.EntitySystem.Stats;

namespace HolyVindicator
{
    internal class Rebalance
    {
        internal static void fixChannelEnergyScaling()
        {
            var empyreal_resource = ResourcesLibrary.TryGetBlueprint<BlueprintAbilityResource>("f9af9354fb8a79649a6e512569387dc5");
            empyreal_resource.SetIncreasedByStat(1, StatType.Wisdom);

            var cleric = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("67819271767a9dd4fbfd4ae700befea0");
            var paladin = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("bfa11238e7ae3544bbeb4d0b92e897ec");
            var sorcerer = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("b3a505fb61437dc4097f43c3f8f9a4cf");

            string[] cleric_channel_ids = new string[] {"f5fc9a1a2a3c1a946a31b320d1dd31b2",
                                                      "279447a6bf2d3544d93a0a39c3b8e91d",
                                                      "9be3aa47a13d5654cbcb8dbd40c325f2",
                                                      "89df18039ef22174b81052e2e419c728"};



            string[] paladin_channel_ids = new string[] { "6670f0f21a1d7f04db2b8b115e8e6abf",
                                                          "0c0cf7fcb356d2448b7d57f2c4db3c0c",
                                                          "4937473d1cfd7774a979b625fb833b47",
                                                          "cc17243b2185f814aa909ac6b6599eaa" };

            string[] empyreal_channel_ids = new string[] { "574cf074e8b65e84d9b69a8c6f1af27b", "e1536ee240c5d4141bf9f9485a665128" };

            foreach (var id in cleric_channel_ids)
            {
                var channel = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(id);
                channel.AddComponent(help.CreateContextCalculateAbilityParamsBasedOnClasses(new BlueprintCharacterClass[] { cleric }, StatType.Charisma));
            }

            foreach (var id in paladin_channel_ids)
            {
                var channel = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(id);
                channel.AddComponent(help.CreateContextCalculateAbilityParamsBasedOnClasses(new BlueprintCharacterClass[] { paladin }, StatType.Charisma));
            }

            foreach (var id in empyreal_channel_ids)
            {
                var channel = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(id);
                channel.AddComponent(help.CreateContextCalculateAbilityParamsBasedOnClasses(new BlueprintCharacterClass[] { sorcerer }, StatType.Charisma));
            }
        }
    }
}
