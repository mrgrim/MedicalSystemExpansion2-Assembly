using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace MSE2.HarmonyPatches
{
    internal static class VREAReactorUninstall
    {
        private static readonly bool VREAisActive;

        private static readonly Type VREAPatchType;
        private static readonly Type VREADefOfType;
        private static readonly Type reactorHediffType;
        private static readonly Type reactorItemType;

        static VREAReactorUninstall()
        {
            VREAisActive = ModLister.AllInstalledMods.Any(m =>
                m.PackageId.EqualsIgnoreCase("vanillaracesexpanded.android") && m.Active);

            if (!VREAisActive) return;

            VREAPatchType = AppDomain.CurrentDomain.GetAssemblies().Reverse()
                .Select(a => a.GetType("VREAndroids.MedicalRecipesUtility_SpawnThingsFromHediffs_Patch"))
                .First(t => t is not null);
            VREADefOfType = AppDomain.CurrentDomain.GetAssemblies().Reverse()
                .Select(a => a.GetType("VREAndroids.VREA_DefOf"))
                .First(t => t is not null);
            reactorHediffType = AppDomain.CurrentDomain.GetAssemblies().Reverse()
                .Select(a => a.GetType("VREAndroids.Hediff_AndroidReactor"))
                .First(t => t is not null);
            reactorItemType = AppDomain.CurrentDomain.GetAssemblies().Reverse()
                .Select(a => a.GetType("VREAndroids.Reactor"))
                .First(t => t is not null);

            if (VREAPatchType is null || VREADefOfType is null || reactorHediffType is null || reactorItemType is null)
                throw new InvalidOperationException("Missing VREA Types in Reverse Patch.");
        }

        // VREA decided they're king of the hill so we can't do this properly with priority. Instead
        // we're forced to nuke their prefix from orbit.
        [HarmonyPatch]
        internal static class Prefix_Patch
        {
            internal static IEnumerable<MethodBase> TargetMethods() => new[] { VREAPatchType.GetMethod("Prefix") };
            internal static bool Prepare(MethodBase original) => original != null || VREAisActive;

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }

        // We have 3 options with 3 downsides here:
        //   * Make VREA a build dependency - Is its own downside
        //   * A bunch of reflection and delegates - A performance hit that may or may not matter
        //   * Reverse harmony patches - Fragile, but FUN!

        [HarmonyPatch]
        internal static class PreFunc_Patch
        {
            static PreFunc_Patch() => MedicalRecipesUtility_SpawnThingsFromHediffs_Patch.compatPreFuncs.Add(PreFunc);
            internal static IEnumerable<MethodBase> TargetMethods() => new[] { VREAPatchType.GetMethod("SpawnThingsFromHediffs") };
            internal static bool Prepare(MethodBase original) => original != null || VREAisActive;

            [HarmonyReversePatch]
            internal static ThingDef PreFunc(Hediff hediff)
            {
                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
                {
                    List<CodeInstruction> instructionList = new(instructions);

                    var start = instructionList.FindIndex(i =>
                        i.Is(OpCodes.Isinst, reactorHediffType));
                    var stop = instructionList.FindIndex(i =>
                        i.Is(OpCodes.Ldsfld, AccessTools.Field(VREADefOfType, "VREA_SpentReactor")));

                    var preList = instructionList.GetRange(start, stop - start + 1);
                    var retNull = il.DefineLabel();

                    preList.Insert(0, CodeInstruction.LoadArgument(0));
                    preList.Add(new CodeInstruction(OpCodes.Ret));
                    preList.Add(new CodeInstruction(OpCodes.Ldnull));
                    preList.Last().labels.Add(retNull);
                    preList.Add(new CodeInstruction(OpCodes.Ret));

                    return preList.Manipulator(
                        ins => ins.Branches(out _),
                        ins => ins.operand = retNull
                    );
                }

                // make compiler happy
                _ = Transpiler(null, null);
                return hediff.def.spawnThingOnRemoved;
            }
        }

        [HarmonyPatch]
        internal static class PostAction_Patch
        {
            static PostAction_Patch() => MedicalRecipesUtility_SpawnThingsFromHediffs_Patch.compatPostActions.Add(PostAction);
            internal static IEnumerable<MethodBase> TargetMethods() => new[] { VREAPatchType.GetMethod("SpawnThingsFromHediffs") };
            internal static bool Prepare(MethodBase original) => original != null || VREAisActive;

            [HarmonyReversePatch]
            internal static void PostAction(Thing thing, Hediff hediff)
            {
                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
                {
                    List<CodeInstruction> instructionList = new(instructions);

                    var start = instructionList.FindIndex(i =>
                        i.Is(OpCodes.Isinst, reactorItemType));
                    var stop = instructionList.FindIndex(i =>
                        i.Is(OpCodes.Stfld, AccessTools.Field(reactorItemType, "curEnergy")));

                    var preList = instructionList.GetRange(start, stop - start + 1);
                    var retLabel = il.DefineLabel();

                    preList.Insert(0, CodeInstruction.LoadArgument(0));
                    preList.Add(new CodeInstruction(OpCodes.Ret));
                    preList.Last().labels.Add(retLabel);

                    return preList.Manipulator(
                        ins => ins.Branches(out _),
                        ins => ins.operand = retLabel
                    ).Manipulator(
                        ins => ins.opcode == OpCodes.Ldloc_S && (ins.operand as LocalVariableInfo)?.LocalIndex == 4,
                        ins => ins.opcode = OpCodes.Ldarg_1
                    );
                }

                // make compiler happy
                _ = Transpiler(null, null);
            }
        }
    }
}