using BTD_Mod_Helper.Api.Data;
using BTD_Mod_Helper.Api.ModOptions;
using UnityEngine;

namespace PlacementHelper
{
    public class Settings : ModSettings
    {
        public static readonly ModSettingHotkey UpHotkey = new(KeyCode.UpArrow)
        {
            description = "Snap the tower upwards to the furthest valid pixel placement. "
        };

        public static readonly ModSettingHotkey DownHotkey = new(KeyCode.DownArrow)
        {
            description = "Snap the tower downwards to the furthest valid pixel placement."
        };

        public static readonly ModSettingHotkey LeftHotkey = new(KeyCode.LeftArrow)
        {
            description = "Snap the tower left to the furthest valid pixel placement."
        };

        public static readonly ModSettingHotkey RightHotkey = new(KeyCode.RightArrow)
        {
            description = "Snap the tower right to the furthest valid pixel placement."
        };
       
        public static readonly ModSettingHotkey NudgeModifierHotkey = new(KeyCode.RightControl)
        {
            description = "While down, nudge by 1 pixel instead of Snapping."
        };

        public static readonly ModSettingBool invertNudgeModifier = new(false)
        {
            description = "Nudge by default and snap when the modifier key is pressed."
        };

        public static readonly ModSettingHotkey SqueezeHotkey = new(KeyCode.LeftShift)
        {
            description = "Squeeze a tower between the closest two towers if both are circular. Subpixel."
        };

        public static readonly ModSettingFloat squeezeOffset = new(0.0001f)
        {
            description = "Added to the tower radius when squeezing to fix floating point issues.",
            min = 0f,
            stepSize = 0.000001f
        };
    }
}
