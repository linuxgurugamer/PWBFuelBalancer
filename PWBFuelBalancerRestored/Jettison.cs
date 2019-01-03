using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Highlighting;

namespace PWBFuelBalancer
{

    public class PWBVesselModule : VesselModule
    {
        [KSPField(isPersistant = true)]
        string resource1 = "";

        [KSPField(isPersistant = true)]
        string resource2 = "";

        [KSPField(isPersistant = true)]
        string resource3 = "";



        const string VALUENAME = "partResource";

        bool highlight = false;

        internal List<string> allPartResources = null;

        internal List<Part> highlightParts = null;

        internal void AddResource(string r)
        {
            if (allPartResources == null)
                allPartResources = new List<string>();
            if (allPartResources.Count < 3)
                allPartResources.Add(r);

            UpdatePersistent();
        }

        internal void RemoveResource(string r)
        {
            allPartResources.Remove(r);
            UpdatePersistent();
        }

        void UpdatePersistent()
        {
            resource1 = resource2 = resource3 = "";
            if (allPartResources.Count >= 1)
                resource1 = allPartResources[0];

            if (allPartResources.Count >= 2)
                resource1 = allPartResources[1];

            if (allPartResources.Count >= 3)
                resource1 = allPartResources[2];
        }

        internal void InitAllPartResources()
        {
            highlightParts = new List<Part>();
            allPartResources = new List<string>();
            if (resource1 != null && resource1 != "")
                allPartResources.Add(resource1);
            if (resource2 != null && resource2 != "")
                allPartResources.Add(resource2);
            if (resource3 != null && resource3 != "")
                allPartResources.Add(resource3);
        }

        public void Clear()
        {
            resource1 = resource2 = resource3 = "";
            allPartResources = new List<string>();
        }

        protected new void Awake()
        {
            //if (HighLogic.LoadedSceneIsGame)
            {
                if (this.vessel != null)
                {
                    InitAllPartResources();
                }
                else
                    Log.Info("PWBVesselModule.Awake, vessel: null");
            }
            base.Awake();
        }

        protected new void Start()
        {
            if (this.vessel != null)
            {
                InitAllPartResources();
            }
            else
                Log.Info("PWBVesselModule.Start, vessel: null");

            UpdateHighlightColors();
            GameEvents.OnGameSettingsApplied.Add(UpdateHighlightColors);
            base.Start();
            StartCoroutine(CycleHighlighting());
        }

        void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(UpdateHighlightColors);
        }

        public void HighlightSinglePart(Color highlightC, Color edgeHighlightColor, Part p)
        {
            p.SetHighlightDefault();
            p.SetHighlightType(Part.HighlightType.AlwaysOn);
            p.SetHighlight(true, false);
            p.SetHighlightColor(highlightC);
            p.highlighter.ConstantOn(edgeHighlightColor);
            p.highlighter.SeeThroughOn();

        }
        public void AddPartToHighlight(Part p)
        {
            if (highlightParts.Contains(p))
                return;
            highlightParts.Add(p);
        }

        public void DisablePartHighlighting(Part part)
        {
            if (highlightParts.Contains(part))
            {
                part.SetHighlightDefault();
                part.SetHighlight(false, false);
                Highlighter highlighter = part.highlighter;
                part.highlighter.ConstantOff();
                part.highlighter.SeeThroughOff();
                highlightParts.Remove(part);
            }
        }

        IEnumerator CycleHighlighting()
        {
            while (true)
            {
                highlight = !highlight;
                if (highlight)
                {
                    HighlightPartsOn();
                }
                else
                {
                    HighlightPartsOff();
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

                Color highlightC = XKCDColors.Black;
                Color edgeHighlightColor = XKCDColors.Black;
        bool highlightActive = false;
        void UpdateHighlightColors()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingBlue ||
                    HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingRed ||
                    HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingGreen)
            {
                highlightActive = true;


                if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingBlue)
                {
                    highlightC.b = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightBlue / 100f;
                    edgeHighlightColor.b = highlightC.b;
                }
                if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingRed)
                {
                    highlightC.r = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightRed / 100f;
                    edgeHighlightColor.r = highlightC.r;
                }
                if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightVentingGreen)
                {
                    highlightC.g = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightGreen / 100f;
                    edgeHighlightColor.g = highlightC.g;
                }
            }
            else
                highlightActive = false;
        }

        void HighlightPartsOn()
        {
            if (highlightActive)
            { 
                for (int i = highlightParts.Count - 1; i >= 0; i--)
                {
                    Part part = highlightParts[i];
                    part.SetHighlightColor(highlightC);
                    part.highlighter.ConstantOn(edgeHighlightColor);
                    part.highlighter.SeeThroughOn();
                }
            }
        }

        void HighlightPartsOff()
        {
            for (int i = highlightParts.Count - 1; i >= 0; i--)
            {
                highlightParts[i].SetHighlightDefault();
                highlightParts[i].SetHighlight(false, false);
                Highlighter highlighter = highlightParts[i].highlighter;
                highlightParts[i].highlighter.ConstantOff();
                highlightParts[i].highlighter.SeeThroughOff();
            }
        }
    }



    public class Jettison : PartModule
    {
        PWBVesselModule pwbVModule;

        [KSPField(isPersistant = true)]
        internal bool jettisonRes1 = false;

        [KSPField(isPersistant = true)]
        internal bool jettisonRes2 = false;

        [KSPField(isPersistant = true)]
        internal bool jettisonRes3 = false;

        List<int> pr = new List<int>();


        void DoJettison(ref bool jettisonRes, int i, bool forceOn = false)
        {
            if (!forceOn)
                jettisonRes = !jettisonRes;
            string eventName = "JettisonRes" + i;
            if (jettisonRes)
                Events[eventName].guiName = "Stop jettisoning " + part.Resources[pr[i - 1]].info.displayName;
            else
            {
                Events[eventName].guiName = "Jettison " + part.Resources[pr[i - 1]].info.displayName;
                pwbVModule.DisablePartHighlighting(this.part);
            }
        }
#if true
        void DoJettisonAll(int resourceNum, bool forceOn = false)
        {
            string resourceName = part.Resources[pr[resourceNum]].resourceName;

            if (!forceOn && pwbVModule.allPartResources.Contains(part.Resources[pr[resourceNum]].resourceName))
            {
                foreach (var p in this.vessel.Parts)
                {
                    var m = p.Modules.GetModule<Jettison>();
                    if (m != null)
                    {
                        for (int r = m.pr.Count - 1; r >= 0; r--)
                        {
                            if (part.Resources[m.pr[r]].resourceName == resourceName) // && part.Resources[m.pr[r]].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Jettison all " + part.Resources[pr[resourceNum]].info.displayName;
                                pwbVModule.DisablePartHighlighting(p);
                                break;
                            }
                        }
                    }
                }
                pwbVModule.RemoveResource(part.Resources[pr[resourceNum]].resourceName);
            }
            else
            {
                foreach (var p in this.vessel.Parts)
                {
                    var m = p.Modules.GetModule<Jettison>();
                    if (m != null)
                    {
                        for (int r = m.pr.Count - 1; r >= 0; r--)
                        {
                            if (part.Resources[m.pr[r]].resourceName == resourceName && part.Resources[m.pr[r]].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Stop jettisoning all " + part.Resources[m.pr[r]].info.displayName;
                            }
                        }
                    }
                }
                if (!pwbVModule.allPartResources.Contains(part.Resources[pr[resourceNum]].resourceName))
                    pwbVModule.AddResource(part.Resources[pr[resourceNum]].resourceName);
            }
        }
#else
        void DoJettisonAll(int resourceNum, bool forceOn = false)
        {
            string resourceName = part.Resources[pr[resourceNum]].resourceName;
            foreach (var p in this.vessel.Parts)
            {
                var m = p.Modules.GetModule<Jettison>();
                if (m != null)
                {
                    for (int r = m.pr.Count - 1; r >= 0; r--)
                    {
                        if (!forceOn && pwbVModule.allPartResources.Contains(part.Resources[pr[resourceNum]].resourceName))
                        {
                            if (part.Resources[m.pr[r]].resourceName == resourceName) // && part.Resources[m.pr[r]].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Jettison all " + part.Resources[pr[resourceNum]].info.displayName;
                                pwbVModule.DisablePartHightlighting(p);
                                break;
                            }

                            pwbVModule.RemoveResource(part.Resources[pr[resourceNum]].resourceName);
                        }
                        else
                        {
                            if (part.Resources[m.pr[r]].resourceName == resourceName && part.Resources[m.pr[r]].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Stop jettisoning all " + part.Resources[m.pr[r]].info.displayName;
                            }

                            if (!pwbVModule.allPartResources.Contains(part.Resources[pr[resourceNum]].resourceName))
                                pwbVModule.AddResource(part.Resources[pr[resourceNum]].resourceName);
                        }
                    }
                }
            }
        }

#endif
        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison Resource")]
        public void JettisonRes1()
        {
            DoJettison(ref jettisonRes1, 1);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison All Resource")]
        public void JettisonAllRes1()
        {
            DoJettisonAll(0);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison Resource")]
        public void JettisonRes2()
        {
            DoJettison(ref jettisonRes2, 2);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison All Resource")]
        public void JettisonAllRes2()
        {
            DoJettisonAll(1);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison Resource")]
        public void JettisonRes3()
        {
            DoJettison(ref jettisonRes3, 3);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Jettison All Resource")]
        public void JettisonAllRes3()
        {
            DoJettisonAll(2);
        }

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || pwbVModule == null)
                return;
            if (jettisonRes1 && !pwbVModule.allPartResources.Contains(part.Resources[pr[0]].resourceName))
                jettisonRes1 = DoJettison(part.Resources[pr[0]]);
            if (jettisonRes2 && !pwbVModule.allPartResources.Contains(part.Resources[pr[1]].resourceName))
                jettisonRes2 = DoJettison(part.Resources[pr[1]]);
            if (jettisonRes3 && !pwbVModule.allPartResources.Contains(part.Resources[pr[2]].resourceName))
                jettisonRes3 = DoJettison(part.Resources[pr[2]]);

            for (int i = pwbVModule.allPartResources.Count - 1; i >= 0; i--)
            {
                for (int i1 = pr.Count - 1; i1 >= 0; i1--)
                {
                    if (pwbVModule.allPartResources[i] == part.Resources[pr[i1]].resourceName)
                    {
                        if (part.Resources[pr[i1]].flowState)
                        {
                            DoJettison(part.Resources[pr[i1]]);
                            break;
                        }
                        else
                        {
                            pwbVModule.DisablePartHighlighting(this.part);
                        }
                    }
                }
            }
        }




        bool DoJettison(PartResource r)
        {
            if (r.amount > 0f)
            {
                r.amount = Math.Max(0, r.amount - r.maxAmount / 2000);

                pwbVModule.AddPartToHighlight(this.part);
            }
            else
                pwbVModule.DisablePartHighlighting(this.part);

            return (r.amount > 0f);
        }

        void DisableJettison()
        {
            jettisonRes1 = false;
            jettisonRes2 = false;
            jettisonRes3 = false;
            pr.Clear();
            if (pwbVModule != null)
            {
                pwbVModule.DisablePartHighlighting(this.part);
                pwbVModule.Clear();
            }
        }

        public void OnGameSceneLoadRequested(GameScenes gc)
        {
            if (!HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().continueThroughSceneChanges)
            {
                DisableJettison();
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);
        }


        public void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
            pwbVModule = this.vessel.GetComponent<PWBVesselModule>();
            if (pwbVModule == null)
            {
                Log.Error("Start, pwbVModule is null");
                return;
            }
            if (pwbVModule.allPartResources == null)
                pwbVModule.InitAllPartResources();

            if (!HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().continueThroughSceneChanges)
            {
                DisableJettison();
            }

            int count = 0;

            for (int rcnt = 0; rcnt <= part.Resources.Count - 1; rcnt++)
            {

                var resource = part.Resources[rcnt];
               
                if (resource.resourceName != "Ore" &&
                    resource.resourceName != "Ablator" &&
                    resource.resourceName != "SolidFuel" &&
                    resource.resourceName != "ElectricCharge")
                {
                    count++;
                    if (count <= 3)
                    {

                        pr.Add(rcnt);
                        string eventName = "JettisonRes" + count;
                        string allEventName = "JettisonAllRes" + count;

                        Events[eventName].active = true;
                        Events[eventName].guiActive = true;
                        //Events[eventName].guiName = "Jettison " + resource.info.displayName;

                        switch (count)
                        {
                            case 1:
                                DoJettison(ref jettisonRes1, 1, true); break;
                            case 2:
                                DoJettison(ref jettisonRes2, 2, true); break;
                            case 3:
                                DoJettison(ref jettisonRes3, 3, true); break;
                        }


                        Events[allEventName].active = true;
                        Events[allEventName].guiActive = true;
                        Events[allEventName].guiName = "Jettison all " + resource.info.displayName;

                        if (pwbVModule.allPartResources.Contains(resource.resourceName))
                        {

                            switch (count)
                            {
                                case 1:
                                    DoJettisonAll(0, true); break;
                                case 2:
                                    DoJettisonAll(1, true); break;
                                case 3:
                                    DoJettisonAll(2, true); break;
                            }
                        }

                    }
                }
            }
        }
    }
}
