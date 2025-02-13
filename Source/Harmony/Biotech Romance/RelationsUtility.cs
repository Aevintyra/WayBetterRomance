﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;

namespace BetterRomance.HarmonyPatches
{
    [HarmonyPatch(typeof(RelationsUtility), "RomanceEligible")]
    public static class RelationsUtility_RomanceEligible
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MinAgeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Is(OpCodes.Ldc_R4, 16f))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call(typeof(SettingsUtilities), nameof(SettingsUtilities.MinAgeForSex));
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AsexualTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo traitDefOf = AccessTools.Field(typeof(TraitDefOf), nameof(TraitDefOf.Asexual));
            bool foundMessageAsexual = false;
            bool foundTraitDefOf = false;
            int startIndex = -1;
            int endIndex = -1;

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ret)
                {
                    if (foundMessageAsexual && foundTraitDefOf)
                    {
                        endIndex = i;
                        break;
                    }
                    else if (foundTraitDefOf)
                    {
                        int middleIndex = i + 1;
                        for (int k = middleIndex; i < codes.Count; k++)
                        {
                            string strOperand = codes[k].operand as string;
                            if (strOperand == "CantRomanceInitiateMessageAsexual")
                            {
                                foundMessageAsexual = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        startIndex = i + 1;

                        for (int j = startIndex; i < codes.Count; j++)
                        {
                            if (codes[j].opcode == OpCodes.Ret)
                            {
                                break;
                            }
                            if (codes[j].LoadsField(traitDefOf))
                            {
                                foundTraitDefOf = true;
                                break;
                            }
                            
                        }
                    }
                }
            }
            if (startIndex > -1 && endIndex > -1)
            {
                codes[startIndex].opcode = OpCodes.Nop;
                codes.RemoveRange(startIndex + 1, endIndex - startIndex);
            }
            return codes.AsEnumerable();
        }
    }
    
    [HarmonyPatch(typeof(RelationsUtility), "RomanceEligiblePair")]
    public static class RelationsUtility_RomanceEligiblePair
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MinAgeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach(CodeInstruction instruction in instructions)
            {
                if (instruction.Is(OpCodes.Ldc_R4, 16f))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return CodeInstruction.Call(typeof(SettingsUtilities), nameof(SettingsUtilities.MinAgeForSex));
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OrientationTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int startIndex = -1;
            int endIndex = -1;
            bool foundStart = false;
            bool foundEnd = false;

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (!foundStart && codes[i].opcode == OpCodes.Ldarg_1)
                {
                    if (codes[i+1].opcode == OpCodes.Ldarg_0)
                    {
                        startIndex = i - 1;
                        foundStart = true;
                    }
                }
                else if (foundStart && !foundEnd && codes[i].opcode == OpCodes.Brtrue_S)
                {
                    endIndex = i -1;
                    foundEnd = true;
                }
            }
            if (startIndex > -1 && endIndex > -1)
            {
                codes.RemoveRange(startIndex, endIndex - startIndex + 1);
            }
            return codes.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(RelationsUtility), "AttractedToGender")]
    public static class RelationsUtility_AttractedToGender
    {
        public static bool Prefix(Pawn pawn, Gender gender, ref bool __result)
        {
            pawn.EnsureTraits();
            Pawn_StoryTracker story = pawn.story;
            if (story != null)
            {
                //This is true for now since they are biromantic
                if (story.traits.HasTrait(TraitDefOf.Asexual))
                {
                    __result = true;
                }
                else if (story.traits.HasTrait(TraitDefOf.Gay))
                {
                    __result = pawn.gender == gender;
                }
                else if (story.traits.HasTrait(RomanceDefOf.Straight))
                {
                    __result = pawn.gender != gender;
                }
                else if (story.traits.HasTrait(TraitDefOf.Bisexual))
                {
                    __result = true;
                }
                else
                {
                    //If they don't have an orientation trait, they are probably a child
                    __result = false;
                }
                return false;
            }
            //Not really sure what it means to not have a story tracker
            __result = false;
            return false;
        }
    }
}
