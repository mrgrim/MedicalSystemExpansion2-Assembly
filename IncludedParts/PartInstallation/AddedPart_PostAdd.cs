using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

using Verse;

namespace MSE2.HarmonyPatches
{
    [HarmonyPatch( typeof( Hediff_AddedPart ) )]
    [HarmonyPatch( nameof( Hediff_AddedPart.PostAdd ) )]
    internal static class AddedPart_PostAdd
    {
        // swap calls to base.postAdd and restorepart
        // this allows to add hediffs in comppostpostadd
        
        public static IEnumerable<CodeInstruction> Transpiler ( IEnumerable<CodeInstruction> instructions )
        {
            List<CodeInstruction> instructionList = new( instructions );

            int baseCallIndex = instructionList.FindIndex( i =>
                i.Calls( typeof( Hediff_Implant ).GetMethod( "PostAdd" ) ) );
            
            // Call takes two parameters, delete setup and call.
            instructionList.RemoveRange( baseCallIndex - 2, 3 );

            return instructionList;
        }

        // We need to extract the base call from the original method to be cable to call the right method in the Postfix
        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        [HarmonyPatch(typeof(Hediff_AddedPart), "PostAdd")]
        public static void baseCall(Hediff_AddedPart __instance, DamageInfo? dinfo)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> instructionList = new( instructions );
                
                int baseCallIndex = instructionList.FindIndex( i =>
                    i.Calls( typeof( Hediff_Implant ).GetMethod( "PostAdd" ) ) );

                return instructionList.GetRange(baseCallIndex - 2, 3).AsEnumerable();
            }
            
            // make compiler happy
            _ = Transpiler(null);
        }

        // Rather than add the call back in the Transpiler we do it in a Postfix to help with compatibility.
        // VREA is an example of a mod that transpiles into this method and adds an early return.
        public static void Postfix(Hediff_AddedPart __instance, DamageInfo? dinfo)
        {
            baseCall(__instance, dinfo );
        }
    }
}
