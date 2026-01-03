using System;
using System.Collections.Generic;

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
        
        private static Action<Hediff_Implant, DamageInfo?> baseCall;

        static AddedPart_PostAdd()
        {
            // Who knows what 1.7 might bring? The delegate needs to know the base type at compile time.
            if (typeof(Hediff_AddedPart).BaseType != typeof( Hediff_Implant ))
                throw new ArgumentException("[MSE2] Hediff_Implant is not base type of Hediff_AddedPart");
            
            var methodInfo = typeof(Hediff_Implant).GetMethod("PostAdd");
            if (methodInfo is null)
                throw new ArgumentException("[MSE2] Hediff_Implant.PostAdd method is not found");
            
            baseCall = (Action<Hediff_Implant, DamageInfo?>)
                Delegate.CreateDelegate(typeof(Action<Hediff_Implant, DamageInfo?>), methodInfo);
        }

        public static IEnumerable<CodeInstruction> Transpiler ( IEnumerable<CodeInstruction> instructions )
        {
            List<CodeInstruction> instructionList = new( instructions );

            int baseCallIndex = instructionList.FindIndex( i =>
                i.Calls( typeof( Hediff_Implant ).GetMethod( "PostAdd" ) ) );
            
            // Call takes two parameters, delete setup and call.
            instructionList.RemoveRange( baseCallIndex - 2, 3 );

            return instructionList;
        }

        // Rather than add the call back in the Transpiler we do it in a Postfix to help with compatibility.
        // VREA is an example of a mod that transpiles into this method and adds an early return.
        public static void Postfix(Hediff_AddedPart __instance, DamageInfo? dinfo)
        {
            baseCall(__instance, dinfo);
        }
    }
}
