﻿using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using AlienRace;

namespace BetterRomance
{
    //This determines how likely a pawn is to want to have sex with another pawn
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "SecondaryLovinChanceFactor")]
    public static class Pawn_RelationsTracker_SecondaryLovinChanceFactor
    {
        //Changes from Vanilla:
        //Allows for non-ideal orientation match ups, at a low rate
        //Allows cross species attraction
        //Adjusted age calculations to be more realistic with regards to young pawns
        //Consideration of target's capabilities
        //Gender age preferences are now the same, except for mild cultural variation.
        //Pawns with Ugly trait are less uninterested romantically in other ugly pawns.
        internal static FieldInfo _pawn;

        public static bool Prefix(Pawn otherPawn, ref float __result, Pawn_RelationsTracker __instance)
        {
            Pawn pawn = __instance.GetPawn();
            if (pawn == otherPawn)
            {
                __result = 0f;
                return false;
            }
            //If at least one of the pawns is not humanlike, don't allow if they're not the same "race"
            //I think this is to weed out animals/mechs while still allowing for different alien races
            if ((!pawn.RaceProps.Humanlike || !otherPawn.RaceProps.Humanlike) && pawn.def != otherPawn.def)
            {
                __result = 0f;
                return false;
            }
            //Applies cross species setting if race def is not the same
            //Also use HAR to determine if they consider each other aliens
            float crossSpecies = 1f;
            if (Settings.HARActive)
            {
                if (SettingsUtilities.AreRacesConsideredXeno(pawn, otherPawn))
                {
                    crossSpecies = pawn.AlienLoveChance() / 100;
                }
            }
            else if (pawn.def != otherPawn.def)
            {
                crossSpecies = pawn.AlienLoveChance() / 100;
            }
            //Not reducing chances to 0, but lowering if gender and sexuality do not match
            //Changing this to what's in romance attempt success chance and then removing it from there
            float sexualityFactor = 1f;
            if (pawn.RaceProps.Humanlike && pawn.story.traits.HasTrait(TraitDefOf.Asexual))
            {
                sexualityFactor = Mathf.Min(pawn.AsexualRating() + 0.15f, 1f);
            }
            if (pawn.RaceProps.Humanlike && pawn.story.traits.HasTrait(TraitDefOf.Gay))
            {
                if (otherPawn.gender != pawn.gender)
                {
                    sexualityFactor = 0.15f;
                }
            }
            if (pawn.RaceProps.Humanlike && pawn.story.traits.HasTrait(RomanceDefOf.Straight))
            {
                if (otherPawn.gender == pawn.gender)
                {
                    sexualityFactor = 0.15f;
                }
            }

            //Calculations based on age of both parties
            float targetMinAgeForSex = otherPawn.MinAgeForSex();
            float pawnMinAgeForSex = pawn.MinAgeForSex();
            float pawnMaxAgeGap = pawn.MaxAgeGap();
            float pawnAge = pawn.ageTracker.AgeBiologicalYearsFloat;
            float targetAge = otherPawn.ageTracker.AgeBiologicalYearsFloat;
            //If either one is too young for sex, do not allow
            if (targetAge < targetMinAgeForSex || pawnAge < pawnMinAgeForSex)
            {
                __result = 0f;
                return false;
            }
            
            float youngestTargetAge = Mathf.Max(pawnMinAgeForSex, pawnAge - (pawnMaxAgeGap * .75f));
            //For humans, this works out to either min of(21 or age-3), or age-8
            //This allows a 16 year old to be attracted to another 16 year old, while still keeping 20 year olds out
            float youngestReasonableTargetAge = Mathf.Max(Mathf.Min(pawnMinAgeForSex + (pawnMaxAgeGap / 8), pawnAge-3), pawnAge - (pawnMaxAgeGap / 5));
            //For humans, this returns 1 if the target's age is within 8 years of the pawn
            //The exception is when the pawn is between 16-21, in which case it returns 1 if the target's age is 8 years above or 3 years below their age
            float targetAgeLikelihood = GenMath.FlatHill(0.15f, youngestTargetAge, youngestReasonableTargetAge, pawnAge + (pawnMaxAgeGap / 5), pawnAge + (pawnMaxAgeGap * .75f), 0.15f, targetAge);
            //This changes chances based on talking/manipulation/moving stats
            float targetBaseCapabilities = 1f;
            targetBaseCapabilities *= Mathf.Lerp(0.2f, 1f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking));
            targetBaseCapabilities *= Mathf.Lerp(0.2f, 1f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
            targetBaseCapabilities *= Mathf.Lerp(0.2f, 1f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
            //Beauty calculations
            int initiatorBeauty = 0;
            int targetBeauty = 0;
            //Decide if beauty stat should be used instead, which is effected by apparel
            if (otherPawn.RaceProps.Humanlike)
            {
                initiatorBeauty = pawn.story.traits.DegreeOfTrait(TraitDefOf.Beauty);
            }

            if (otherPawn.RaceProps.Humanlike)
            {
                targetBeauty = otherPawn.story.traits.DegreeOfTrait(TraitDefOf.Beauty);
            }
            //Maybe change this to check the difference between the two beauty scores?
            //If target is ugly, reduce chances, less if initiator is also ugly
            float targetBeautyMod = 1f;
            if (targetBeauty == -2)
            {
                targetBeautyMod = initiatorBeauty >= 0 ? 0.3f : 0.8f;
            }
            if (targetBeauty == -1)
            {
                targetBeautyMod = initiatorBeauty >= 0 ? 0.75f : 0.9f;
            }
            //If target is pretty/beautiful, increase chances
            if (targetBeauty == 1)
            {
                targetBeautyMod = 1.7f;
            }
            else if (targetBeauty == 2)
            {
                targetBeautyMod = 2.3f;
            }

            __result = targetAgeLikelihood * targetBaseCapabilities * targetBeautyMod 
                * crossSpecies * sexualityFactor;
            return false;
        }
        //Not sure how this works, but it supplies the pawn argument from the Pawn_RelationsTracker, which is used in SecondaryLovinChanceFactor
        private static Pawn GetPawn(this Pawn_RelationsTracker _this)
        {
            bool flag = _pawn == null;
            if (!flag)
            {
                return (Pawn)_pawn.GetValue(_this);
            }

            _pawn = typeof(Pawn_RelationsTracker).GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);
            bool flag2 = _pawn == null;
            if (flag2)
            {
                Log.Error("Unable to reflect Pawn_RelationsTracker.pawn!");
            }

            return (Pawn)_pawn?.GetValue(_this);
        }
    }
    //This is used to determine chances of deep talk, slight, and insult interaction
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "CompatibilityWith")]
    public static class Pawn_RelationsTracker_CompatibilityWith
    {
        //Change from Vanilla:
        //Age adjustments
        internal static FieldInfo _pawn;
        public static bool Prefix(Pawn otherPawn, ref float __result, ref Pawn_RelationsTracker __instance)
        {
            Pawn pawn = __instance.GetPawn();
            if (pawn.def != otherPawn.def || pawn == otherPawn)
            {
                __result = 0f;
                return false;
            }
            float ageGap = Mathf.Abs(pawn.ageTracker.AgeBiologicalYearsFloat - otherPawn.ageTracker.AgeBiologicalYearsFloat);
            float num = Mathf.Clamp(GenMath.LerpDouble(0f, pawn.MaxAgeGap() /2, 0.45f, -0.45f, ageGap), -0.45f, 0.45f);
            float num2 = __instance.ConstantPerPawnsPairCompatibilityOffset(otherPawn.thingIDNumber);
            __result = num + num2;
            return false;
        }

        private static Pawn GetPawn(this Pawn_RelationsTracker _this)
        {
            bool flag = _pawn == null;
            if (!flag)
            {
                return (Pawn)_pawn.GetValue(_this);
            }

            _pawn = typeof(Pawn_RelationsTracker).GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);
            bool flag2 = _pawn == null;
            if (flag2)
            {
                Log.Error("Unable to reflect Pawn_RelationsTracker.pawn!");
            }

            return (Pawn)_pawn?.GetValue(_this);
        }
    }
}