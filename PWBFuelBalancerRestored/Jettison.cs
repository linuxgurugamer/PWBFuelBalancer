using System;
using System.Collections.Generic;


namespace PWBFuelBalancer
{
    public class Jettison : PartModule
    {
        internal bool jettisonRes1 = false;
        internal bool jettisonRes2 = false;
        internal bool jettisonRes3 = false;

        List<int> pr = new List<int>();
        static List<string> allPartResources = null;
        

        void DoJettison(ref bool jettisonRes, int i)
        {
            jettisonRes = !jettisonRes;
            string eventName = "JettisonRes" + i;
            if (jettisonRes)
                Events[eventName].guiName = "Stop jettisoning " + part.Resources[pr[i - 1]].info.displayName;
            else
                Events[eventName].guiName = "Jettison " + part.Resources[pr[i - 1]].info.displayName;
        }
        
        void DoJettisonAll(int resourceNum)
        {
            string resourceName = part.Resources[pr[resourceNum]].resourceName;

            if (allPartResources.Contains(part.Resources[pr[resourceNum]].resourceName))
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
                                m.Events[eventName].guiName = "Jettison all " + part.Resources[pr[resourceNum]].info.displayName;
                            }
                        }
                    }
                }
                allPartResources.Remove(part.Resources[pr[resourceNum]].resourceName);
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
                allPartResources.Add(part.Resources[pr[resourceNum]].resourceName);
            }
        }

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
        int i0 = 0;
        public void LateUpdate()
        {
            if (jettisonRes1 && !allPartResources.Contains(part.Resources[pr[0]].resourceName))
                jettisonRes1 = DoJettison(part.Resources[pr[0]]);
            if (jettisonRes2 && !allPartResources.Contains(part.Resources[pr[1]].resourceName))
                jettisonRes2 = DoJettison(part.Resources[pr[1]]);
            if (jettisonRes3 && !allPartResources.Contains(part.Resources[pr[2]].resourceName))
                jettisonRes3 = DoJettison(part.Resources[pr[2]]);
            i0++;
            Log.Info("LateUpdate, i0: " + i0 + ",  part: " + this.part.partInfo.title + ", allPartResources.Count: " + allPartResources.Count + ", partResources.Count: " + pr.Count);
            for (int i = allPartResources.Count - 1; i >= 0; i--)
            {
                for (int i1 = pr.Count - 1; i1 >= 0; i1--)
                {
                    if (i0 == 20)
                    {
                        Log.Info("LateUpdate, allPartResources[i]: " + allPartResources[i] + ", partResources[i1].resourceName: " + part.Resources[pr[i1]].resourceName +
                            ", flowState: " + part.Resources[pr[i1]].flowState + ", _flowState: " + part.Resources[pr[i1]]._flowState);
                        i0 = 0;
                    }
                    if (allPartResources[i] == part.Resources[pr[i1]].resourceName && part.Resources[pr[i1]].flowState)
                    {
                        DoJettison(part.Resources[pr[i1]]);
                        break;
                    }
                }
            }
        }


        bool DoJettison(PartResource r)
        {
            Log.Info("DoJettison, partResource: " + r.info.displayName + ", r.amount: " + r.amount);
            if (r.amount > 0f)
            {
                r.amount -= Math.Max(0, r.maxAmount / 2000);
            }
            return (r.amount > 0f);
        }


        public override void OnStart(StartState state)
        {
            if (allPartResources == null)
            {
                Log.Info("OnStart, allPartResources init");
                allPartResources = new List<string>();
            }
            int count = 0;

            for (int r = part.Resources.Count - 1; r >= 0; r--)
            //foreach (PartResource resource in part.Resources)
            {
                var resource = part.Resources[r];
                if (resource.resourceName != "Ore" &&
                    resource.resourceName != "Ablator" &&
                    resource.resourceName != "SolidFuel" &&
                    resource.resourceName != "ElectricCharge")
                {
                    count++;
                    if (count <= 3)
                    {
                        pr.Add(r);
                        string eventName = "JettisonRes" + count;
                        string allEventName = "JettisonAllRes" + count;

                        Events[eventName].active = true;
                        Events[eventName].guiActive = true;
                        Events[eventName].guiName = "Jettison " + resource.info.displayName;

                        Events[allEventName].active = true;
                        Events[allEventName].guiActive = true;
                        Events[allEventName].guiName = "Jettison all " + resource.info.displayName;
                    }
                }
            }
        }
    }
}
