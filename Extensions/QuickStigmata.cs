using HarmonyLib;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;

// Stolen, with permission, from @microsoftenator in the Owlcat Games discord. Thank you!

namespace HolyVindicator
{
    internal class UnitPartStigmataMove : OldStyleUnitPart
    {
        static readonly UnitCommand.CommandType CommandType = UnitCommand.CommandType.Move;
        internal UnitCommand.CommandType GetCommandType () { return CommandType; }
    }

    internal class UnitPartStigmataSwift : OldStyleUnitPart
    {
        static readonly UnitCommand.CommandType CommandType = UnitCommand.CommandType.Swift;
        internal UnitCommand.CommandType GetCommandType() { return CommandType; }

    }

    internal class QuickenStigmataMove : UnitFactComponentDelegate
    {
        public override void OnTurnOff()
        {
            Main.Print("Turning off Move Stigmata");
            base.Owner.Remove<UnitPartStigmataMove>();
        }

        public override void OnTurnOn()
        {
            Main.Print("Turning On Move Stigmata");
            base.Owner.Ensure<UnitPartStigmataMove>();
        }
    }

    internal class QuickenStigmataSwift : UnitFactComponentDelegate
    {
        public override void OnTurnOff()
        {
            Main.Print("Turning off Swift Stigmata");
            base.Owner.Remove<UnitPartStigmataSwift>();
        }

        public override void OnTurnOn()
        {
            Main.Print("Turning On Swift Stigmata");
            base.Owner.Ensure<UnitPartStigmataSwift>();
        }
    }

    [HarmonyPatch(typeof(UnitActivateAbility), nameof(UnitActivateAbility.GetCommandType))]
    internal class QuickStigmata
    {
        private static readonly ActivatableAbilityGroup group = HolyVindicator.StigmataAbilityGroup;

        static void Postfix(ActivatableAbility ability, ref UnitCommand.CommandType __result)
        {
            if (ability.Blueprint.Group == group)
            {
                var part1 = ability.Owner.Get<UnitPartStigmataMove>();
                if (part1 is not null)
                    __result = part1.GetCommandType();
                var part2 = ability.Owner.Get<UnitPartStigmataSwift>();
                if (part2 is not null)
                    __result = part2.GetCommandType();
            }
        }
    }
}