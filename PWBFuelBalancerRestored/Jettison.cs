using System;
using System.Collections.Generic;


namespace PWBFuelBalancer
{
    public class Jettison : PartModule
    {
        internal bool jettisonRes1 = false;
        internal bool jettisonRes2 = false;
        internal bool jettisonRes3 = false;

        List<PartResource> partResources = new List<PartResource>();
        static List<string> allPartResources = null;
        

        void DoJettison(ref bool jettisonRes, int i)
        {
            jettisonRes = !jettisonRes;
            string eventName = "JettisonRes" + i;
            if (jettisonRes)
                Events[eventName].guiName = "Stop jettisoning " + partResources[i - 1].info.displayName;
            else
                Events[eventName].guiName = "Jettison " + partResources[i - 1].info.displayName;
        }
        
        void DoJettisonAll(int resourceNum)
        {
            string resourceName = this.partResources[resourceNum].resourceName;

            if (allPartResources.Contains(partResources[resourceNum].resourceName))
            {
                foreach (var p in this.vessel.Parts)
                {
                    var m = p.Modules.GetModule<Jettison>();
                    if (m != null)
                    {
                        for (int r = m.partResources.Count - 1; r >= 0; r--)
                        {
                            if (m.partResources[r].resourceName == resourceName && m.partResources[r].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Jettison all " + this.partResources[resourceNum].info.displayName;
                            }
                        }
                    }
                }
                allPartResources.Remove(partResources[resourceNum].resourceName);
            }
            else
            {
                foreach (var p in this.vessel.Parts)
                {
                    var m = p.Modules.GetModule<Jettison>();
                    if (m != null)
                    {
                        for (int r = m.partResources.Count - 1; r >= 0; r--)
                        {
                            if (m.partResources[r].resourceName == resourceName && m.partResources[r].flowState)
                            {
                                string eventName = "JettisonAllRes" + (r + 1);
                                m.Events[eventName].guiName = "Stop jettisoning all " + m.partResources[r].info.displayName;
                            }
                        }
                    }
                }
                allPartResources.Add(partResources[resourceNum].resourceName);
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

        public void LateUpdate()
        {
            if (jettisonRes1 && !allPartResources.Contains(partResources[0].resourceName))
                jettisonRes1 = DoJettison(partResources[0]);
            if (jettisonRes2 && !allPartResources.Contains(partResources[1].resourceName))
                jettisonRes2 = DoJettison(partResources[1]);
            if (jettisonRes3 && !allPartResources.Contains(partResources[2].resourceName))
                jettisonRes3 = DoJettison(partResources[2]);

            for (int i = allPartResources.Count - 1; i >= 0; i--)
            {
                for (int i1 = partResources.Count - 1; i1 >= 0; i1--)
                {
                    if (allPartResources[i] == partResources[i1].resourceName && partResources[i1].flowState)
                    {
                        DoJettison(partResources[i1]);
                        break;
                    }
                }
            }
        }


        bool DoJettison(PartResource r)
        {
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

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName != "Ore" &&
                    resource.resourceName != "Ablator" &&
                    resource.resourceName != "SolidFuel" &&
                    resource.resourceName != "ElectricCharge")
                {
                    count++;
                    if (count <= 3)
                    {
                        partResources.Add(resource);
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
