using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Highlighting;
using KSP_PartHighlighter;
using KSP_Log;

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


        internal static Log Log = new Log("PWBFuelBalancer.PWBVesselModule");

        const string VALUENAME = "partResource";

        //bool highlight = false;

        internal List<string> allPartResources = null;

        internal List<Part> highlightParts = null;
        internal PartHighlighter phl = null;
        internal int highlightID;

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
            phl = PartHighlighter.CreatePartHighlighter();
            if (phl == false)
                return;
            highlightID = phl.CreateHighlightList();
            if (highlightID < 0)
                return;

            if (this.vessel != null)
            {
                InitAllPartResources();
            }
            else
                Log.Info("PWBVesselModule.Start, vessel: null");

            UpdateHighlightColors();
            GameEvents.OnGameSettingsApplied.Add(UpdateHighlightColors);
            base.Start();
        }

        void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(UpdateHighlightColors);
        }

        void UpdateHighlightColors()
        {

            {
                //highlightActive = true;

                Color c = new Color(1,1,1,1);
                // The following code is because the old way of storing the colors was a number from 0-100, 
                // The new ColorPicker uses a number 0-1

                c.b = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightBlue > 1 ? HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightBlue / 100f : HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightBlue;
                c.r = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightRed > 1 ? HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightRed / 100f : HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightRed;
                c.g = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightGreen > 1 ? HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightGreen / 100f : HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().highlightGreen;
  
                phl.UpdateHighlightColors(highlightID, c);

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

        internal static Log Log = new Log("PWBFuelBalancer.Jettison");

        public class PartRes
        {
            public int resnum;
            public string resname;

            public PartRes(int i, string s)
            {
                resnum = i;
                resname = s;
            }
        }
        List<PartRes> partRes = new List<PartRes>();
        #region KSPEvents
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
        #endregion


        void DoJettison(ref bool jettisonRes, int i, bool forceOn = false)
        {
            if (!forceOn)
                jettisonRes = !jettisonRes;
            string eventName = "JettisonRes" + i;
            if (Events.Contains(eventName) && i>0 && i <= partRes.Count)
            {
                var resnum = partRes[i - 1].resnum;
                if (resnum >= 0 && resnum < part.Resources.Count)
                {
                    var displayName = part.Resources[resnum].info.displayName;

                    if (jettisonRes)
                        Events[eventName].guiName = "Stop jettisoning " + displayName;
                    else
                    {
                        Events[eventName].guiName = "Jettison " + displayName;
                        if (pwbVModule != null && pwbVModule.phl != null)
                            pwbVModule.phl.DisablePartHighlighting(pwbVModule.highlightID, this.part);
                    }
                }
            }
        }

        void DoJettisonAll(int resourceNum, bool forceOn = false)
        {

            string resourceName = part.Resources[partRes[resourceNum].resnum].resourceName;
            if (!forceOn && pwbVModule.allPartResources.Contains(part.Resources[partRes[resourceNum].resnum].resourceName))
            {
                foreach (var part in this.vessel.Parts)
                {
                    var partModule = part.Modules.GetModule<Jettison>();
                    if (partModule != null)
                    {
                        for (int r = partModule.partRes.Count - 1; r >= 0; r--)
                        {
                            if (partModule.partRes[r].resname == resourceName)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                partModule.Events[eventName].guiName = "Jettison all " + base.part.Resources[partRes[resourceNum].resnum].info.displayName;
                                pwbVModule.phl.DisablePartHighlighting(pwbVModule.highlightID, part);
                                break;
                            }
                        }
                    }
                }
                pwbVModule.RemoveResource(part.Resources[partRes[resourceNum].resnum].resourceName);
            }
            else
            {
                foreach (var part in this.vessel.Parts)
                {
                    var partModule = part.Modules.GetModule<Jettison>();
                    if (partModule != null)
                    {
                        for (int r = partModule.partRes.Count - 1; r >= 0; r--)
                        {
                            if (partModule.partRes[r].resname == resourceName)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                partModule.Events[eventName].guiName = "Stop jettisoning all " + base.part.Resources[partModule.partRes[r].resnum].info.displayName;
                                break;
                            }
                        }
                    }
                }

                if (!pwbVModule.allPartResources.Contains(part.Resources[partRes[resourceNum].resnum].resourceName))
                    pwbVModule.AddResource(part.Resources[partRes[resourceNum].resnum].resourceName);
            }
        }

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || pwbVModule == null)
                return;
            if (jettisonRes1 && !pwbVModule.allPartResources.Contains(part.Resources[partRes[0].resnum].resourceName))
                jettisonRes1 = DoJettison(part.Resources[partRes[0].resnum]);
            if (jettisonRes2 && !pwbVModule.allPartResources.Contains(part.Resources[partRes[1].resnum].resourceName))
                jettisonRes2 = DoJettison(part.Resources[partRes[1].resnum]);
            if (jettisonRes3 && !pwbVModule.allPartResources.Contains(part.Resources[partRes[2].resnum].resourceName))
                jettisonRes3 = DoJettison(part.Resources[partRes[2].resnum]);

            for (int i = pwbVModule.allPartResources.Count - 1; i >= 0; i--)
            {
                for (int i1 = partRes.Count - 1; i1 >= 0; i1--)
                {
                    if (pwbVModule.allPartResources[i] == part.Resources[partRes[i1].resnum].resourceName)
                    {
                        if (part.Resources[partRes[i1].resnum].flowState)
                        {
                            DoJettison(part.Resources[partRes[i1].resnum]);
                            break;
                        }
                        else
                        {
                            pwbVModule.phl.DisablePartHighlighting(pwbVModule.highlightID, this.part);
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

                pwbVModule.phl.AddPartToHighlight(pwbVModule.highlightID, this.part);
            }
            else
                pwbVModule.phl.DisablePartHighlighting(pwbVModule.highlightID, this.part);

            return (r.amount > 0f);
        }

        void DisableJettison()
        {
            jettisonRes1 = false;
            jettisonRes2 = false;
            jettisonRes3 = false;
            partRes.Clear();
            if (pwbVModule != null)
            {
                pwbVModule.phl.DisablePartHighlighting(pwbVModule.highlightID, this.part);
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
                        Log.Info("Part: " + part.partInfo.title + ", Adding to pr: " + rcnt);
                        partRes.Add(new PartRes(rcnt, resource.resourceName));
                        string eventName = "JettisonRes" + count;
                        string allEventName = "JettisonAllRes" + count;

                        Events[eventName].active = true;
                        Events[eventName].guiActive = true;

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
