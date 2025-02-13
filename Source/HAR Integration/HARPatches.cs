﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AlienRace;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRomance

{
    public static class HARPatches
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PatchHAR()
        {
            Harmony harmony = new Harmony(id: "rimworld.divineDerivative.HARinterference");
            harmony.Unpatch(typeof(Pawn_RelationsTracker).GetMethod("CompatibilityWith"), typeof(AlienRace.HarmonyPatches).GetMethod("CompatibilityWithPostfix"));
            harmony.Patch(AccessTools.Method(typeof(AlienRace.HarmonyPatches), nameof(AlienRace.HarmonyPatches.LovinInterval)), prefix: new HarmonyMethod(typeof(HARPatches), nameof(HARPatches.LovinInternalPrefix)));
        }

        public static bool LovinInternalPrefix(SimpleCurve humanDefault, Pawn pawn, ref SimpleCurve __result)
        {
            if (pawn.def is ThingDef_AlienRace alienProps)
            {
                __result = alienProps.alienRace.generalSettings.lovinIntervalHoursFromAge ?? pawn.GetLovinCurve();
            }
            else
            {
                __result = pawn.GetLovinCurve();
            }
            return false;
        }
    }
}
