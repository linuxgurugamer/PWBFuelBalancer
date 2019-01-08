using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PWBFuelBalancer
{
    // http://forum.kerbalspaceprogram.com/index.php?/topic/147576-modders-notes-for-ksp-12/#comment-2754813
    // search for "Mod integration into Stock Settings

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

        [GameParameters.CustomParameterUI("Highlight venting parts red")]
        public bool highlightVentingRed = true;

        [GameParameters.CustomFloatParameterUI("Red value", minValue = 0, maxValue = 100f, stepCount = 101,
            toolTip ="Amount of red to be used in the highlight. range is from 0-100")]
        public float highlightRed = 1f;

        [GameParameters.CustomParameterUI("Highlight venting parts green")]
        public bool highlightVentingGreen = true;

        [GameParameters.CustomFloatParameterUI("Green value", minValue = 0, maxValue = 100f, stepCount = 101,
            toolTip = "Amount of green to be used in the highlight. range is from 0-100")]
        public float highlightGreen = 1f;

        [GameParameters.CustomParameterUI("Highlight venting parts blue")]
        public bool highlightVentingBlue = true;

        [GameParameters.CustomFloatParameterUI("Blue value", minValue = 0, maxValue = 100f, stepCount = 101,
            toolTip = "Amount of blue to be used in the highlight. range is from 0-100")]
        public float highlightBlue = 1f;


        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {            
        }

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (!highlightVentingBlue) highlightBlue = 0;
            if (!highlightVentingGreen) highlightGreen = 0;
            if (!highlightVentingRed) highlightRed = 0;
            if (member.Name == "highlightRed") return highlightVentingRed;
            if (member.Name == "highlightGreen") return highlightVentingGreen;
            if (member.Name == "highlightBlue") return highlightVentingBlue;

            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }
    }
}
