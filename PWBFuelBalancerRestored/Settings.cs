using System.Collections;
using System.Reflection;
using UnityEngine;
using KSPColorPicker;

namespace PWBFuelBalancer
{
    // http://forum.kerbalspaceprogram.com/index.php?/topic/147576-modders-notes-for-ksp-12/#comment-2754813
    // search for "Mod integration into Stock Settings
    //
    //  HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().

    public class PWBSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return ""; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "PWB Fuel Balancer"; } }
        public override string DisplaySection { get { return "PWB Fuel Balancer"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Use KSP Skin")]
        public bool useKSPskin = false;

        [GameParameters.CustomParameterUI("Jettisoning fuel continues even when scene changes")]
        public bool continueThroughSceneChanges = true;

        [GameParameters.CustomParameterUI("Show toggles in PAW")]
        public bool showTogglesInPaw = false;


        [GameParameters.CustomParameterUI("Show Color Picker",
           toolTip = "Show the Color Picker dialog")]
        public bool showColorPicker = false;


        [GameParameters.CustomFloatParameterUI("Red value", minValue = 0, maxValue = 100f, stepCount = 101, displayFormat = "F4",
            toolTip ="Amount of red to be used in the highlight. range is from 0-100")]
        public float highlightRed = 1f;

        [GameParameters.CustomFloatParameterUI("Green value", minValue = 0, maxValue = 100f, stepCount = 101, displayFormat = "F4",
            toolTip = "Amount of green to be used in the highlight. range is from 0-100")]
        public float highlightGreen = 1f;

        [GameParameters.CustomFloatParameterUI("Blue value", minValue = 0, maxValue = 100f, stepCount = 101, displayFormat = "F4",
            toolTip = "Amount of blue to be used in the highlight. range is from 0-100")]
        public float highlightBlue = 1f;

        [GameParameters.CustomIntParameterUI("Center of Lift Cutoff",minValue = 1, maxValue = 10,
            toolTip = "Lower value sets the arrow sooner and keeps it on longer.")]
        public int LiftCutoff = 10;

        [GameParameters.CustomIntParameterUI("Max Arrow length", minValue = 1, maxValue = 10)]
        public int ArrowLength = 4;

        [GameParameters.CustomIntParameterUI("Max Arrow top speed (m/sec)", minValue = 10, maxValue = 300,
            toolTip = "The arrow will change size depending on the speed.  This speed and higher will have the arrow at the Max Arrow length")]
        public int MaxArrowTopSpeed = 75;


        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {            
        }

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (member.Name == "highlightRed" || member.Name == "highlightGreen" || member.Name == "highlightBlue") return false;

            if (showColorPicker)
            {
                showColorPicker = false;
                Color c = new Color(1, 1, 1, 1);
                c.b = highlightBlue;
                c.g = highlightGreen;
                c.r = highlightRed;
                KSP_ColorPicker.CreateColorPicker(c, false, "ColorCircle");
            }
            return true;
        }

        bool unread = false;
        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            if (KSP_ColorPicker.showPicker)
            {
                unread = true;
                KSP_ColorPicker.colorPickerInstance.PingTime();
                return false;
            }
            else
            {
                if (KSP_ColorPicker.success && unread)
                {
                    unread = false;
                    highlightBlue = KSP_ColorPicker.SelectedColor.b;
                    highlightGreen = KSP_ColorPicker.SelectedColor.g;
                    highlightRed = KSP_ColorPicker.SelectedColor.r;
                }
            }
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }
    }
}
