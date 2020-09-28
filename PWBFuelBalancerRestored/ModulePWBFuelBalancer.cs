using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP_Log;

namespace PWBFuelBalancer
{
    internal class SavedMarker
    {
        internal string name;
        internal Vector3 marker;

        internal SavedMarker(string name, Vector3 marker)
        {
            this.name = name;
            this.marker = marker;
        }
    }
    public class ModulePWBFuelBalancer : PartModule
    {
        private ArrayList _tanks;
        private int _iNextSourceTank;
        private int _iNextDestinationTank;
        private float _fMostMovedThisRound;
        internal GameObject SavedCoMMarker;
        GameObject ActualCoMMarker;
        GameObject ActualCoLMarker;
        public bool MarkerVisible;
        public bool SavedCoMMarkerVisible;
        public bool ActualCoMMarkerVisible;
        public bool ActualCoLMarkerVisible;

        internal List<ModulePWBFuelBalancer> pwbModules = new List<ModulePWBFuelBalancer>();

        static Log Log = new Log("PWBFuelBalancer.ModulePWBFuelBalancer");

        private bool _started; // used to tell if we are set up and good to go. The Update method will check this know if it is a good idea to try to go anything or not.
        private DateTime _lastKeyInputTime;

        internal bool HideUI { get; set; }


        static internal Vector3 NegVector = new Vector3(-1, -1, -1);
        [KSPField]
        internal bool isPWBFuelBalancerPart = false;

        [KSPField]
        public string SetMassKey = "m";

        [KSPField]
        public string DisplayMarker = "d";

        [KSPField(isPersistant = true)]
        public Vector3 VecFuelBalancerCoMTarget = NegVector;

        [KSPField(isPersistant = true)]
        public string BalancerName = null;

        [KSPField(isPersistant = true)]
        public float maxVal = 10f;

        [KSPField(isPersistant = true)]
        public string Save1Name = "Save1";

        [KSPField(isPersistant = true)]
        public Vector3 VecSave1CoMTarget;

        [KSPField(isPersistant = true)]
        public string Save2Name = "Save2";

        [KSPField(isPersistant = true)]
        public Vector3 VecSave2CoMTarget;


        internal List<SavedMarker> savedMarkers = new List<SavedMarker>();


        [KSPField(isPersistant = true)]
        public Quaternion RotationInEditor;

        internal enum BalanceStatus { Deactivated = 0, Balance_not_possible = 1, Maintaining = 2, Balancing = 3, Standby = 4 };
        internal BalanceStatus balanceStatus;

        [KSPField(isPersistant = false, guiActive = true, guiName = "PWB Fuel Balancer Status")]
        public string Status;

        [KSPField(isPersistant = false, guiActive = true, guiName = "PWB Fuel Balance: CoM Error", guiUnits = "m", guiFormat = "f3")]
        public float FComError;

        [KSPAction("PWB Fuel Balancer: Balance Fuel Tanks")]
        public void BalanceFuelAction(KSPActionParam param)
        {
            BalanceFuel();
        }

        [KSPEvent(guiActive = true, guiName = "Show PWB Dialog", active = true)]
        public void ShowPWBDialog()
        {
            PwbFuelBalancerAddon.fetch.OnAppLaunchToggle();
        }

        [KSPEvent(guiActive = true, guiName = "PWB Fuel Balancer: Deactivate", active = false)]
        public void Disable()
        {
            balanceStatus = BalanceStatus.Deactivated;
            _tanks = null;
            Status = "Deactivated";
            Events["Disable"].active = false;
            Events["BalanceFuel"].active = true;
            Events["Maintain"].active = true;

            // Clear the list of tanks. They will have to be rebuilt next time balancing is enabled
        }

        [KSPEvent(guiActiveEditor = true, guiName = "PWB Fuel Balancer: Set Center of Mass")]
        internal void SetCoM()
        {
            if (SetCoMTarget())
                ScreenMessages.PostScreenMessage("CoM Target Set", 5);
            ToggleSavedMarker(true);
        }

        void ResetfNextAmountMoved()
        {
            for (int i = 0; i < _tanks.Count - 1; i++)
                ((PartAndResource)_tanks[i]).fNextAmountMoved = ((PartAndResource)_tanks[i]).initialTransferRate;
        }

        [KSPEvent(guiActive = true, guiName = "PWB Fuel Balancer: Keep Balanced", active = true)]
        public void Maintain()
        {
            // If we were previously Deactivated then we need to build a list of tanks and set up to start balancing
            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible)
            {
                BuildTanksList();

                _iNextSourceTank = 0;
                _iNextDestinationTank = 0;
                ResetfNextAmountMoved();
                _fMostMovedThisRound = 0;
                DeactivateOtherPWBModules();
            }

            Events["Disable"].active = true;
            Events["BalanceFuel"].active = false;
            Events["Maintain"].active = false;
            balanceStatus = BalanceStatus.Maintaining;
            Status = "Maintaining";
        }

        void DeactivateModule(ModulePWBFuelBalancer m)
        {
            m.balanceStatus = BalanceStatus.Deactivated;
            m.Status = "Deactivated";
            m.Events["Disable"].active = false;
            m.Events["BalanceFuel"].active = true;
            m.Events["Maintain"].active = true;

        }
        void DeactivateOtherPWBModules()
        {
            foreach (var m in pwbModules)
            {
                if (m != this)
                {
                    DeactivateModule(m);
                }
            }
        }

        [KSPEvent(guiActive = true, guiName = "PWB Fuel Balancer: Balance Fuel (one time)", active = true)]
        public void BalanceFuel()
        {
            // If we were previousyl deactive, then we need to build the list of tanks and set up to start balancing
            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible)
            {
                BuildTanksList();

                _iNextSourceTank = 0;
                _iNextDestinationTank = 0;
                ResetfNextAmountMoved();

                _fMostMovedThisRound = 0;

                DeactivateOtherPWBModules();
            }
            Events["Disable"].active = true;
            Events["BalanceFuel"].active = false;
            Events["Maintain"].active = false;
            balanceStatus = BalanceStatus.Balancing;
            Status = "Balancing";
        }

        private void BuildTanksList()
        {
            // Go through all the parts and get a list of the tanks which should save us some bother
            //Log.Info("building a tank list");

            _tanks = new ArrayList();
            IEnumerator<Part> parts = vessel.Parts.GetEnumerator();
            while (parts.MoveNext())
            {
                if (parts.Current == null) continue;
                // Step over all resources in this tank.
                IEnumerator resources = parts.Current.Resources.GetEnumerator();
                while (resources.MoveNext())
                {
                    if (resources.Current == null) continue;
                    if (((PartResource)resources.Current).info.density > 0)
                    { // Only consider resources that have mass (don't move electricity!)
                        _tanks.Add(new PartAndResource(parts.Current, (PartResource)resources.Current));
                    }
                }
            }
        }

        /// <summary>
        /// Constructor style setup.
        /// The model may not be built by this point.
        /// </summary>
        public new void Awake()
        {
#if DEBUG
            Log.SetLevel(Log.LEVEL.INFO);
#else
                Log.SetLevel(Log.LEVEL.ERROR);
#endif

            Log.Info("Awake");
            _tanks = null;
            MarkerVisible = false;
            SavedCoMMarkerVisible = false;
            ActualCoMMarkerVisible = false;
            base.Awake();
        }

        public void Start()
        {
            Log.Info("Start");
            Events["Disable"].guiActive =
                Events["Maintain"].guiActive =
                Events["BalanceFuel"].guiActive =
                Events["ToggleAllMarkers"].guiActive =
                Events["ToggleSavedMarker"].guiActive =
                Events["ToggleActualMarker"].guiActive =
                Events["ToggleCoLMarker"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().showTogglesInPaw;


            Events["ShowPWBDialog"].guiActive = !HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().showTogglesInPaw;

            // Initialize the status to be deactivated
            DeactivateModule(this);

            SavedCoMMarker = null; // marker to display the saved location

            _lastKeyInputTime = DateTime.Now;

            FindAllPWBModules();
            BalancerName = (BalancerName == null || BalancerName.Trim() == "") ? part.partInfo.title : BalancerName;
            isPWBFuelBalancerPart = (part.partInfo.name == "PWBFuelBalancer");
            if (HighLogic.LoadedSceneIsEditor)
            {
                RotationInEditor = part.transform.rotation;
            }

            CreateMarkers("Start, part: " + part.partInfo.title + ", name: " + BalancerName);

            _started = true;
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartPlaced.Add(OnEditorPartPlaced);
                GameEvents.onEditorPartDeleted.Add(OnEditorPartDeleted);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onHideUI.Add(OnHideUI);
                GameEvents.onShowUI.Add(OnShowUI);
            }

        }

        public void OnDestroy()
        {
            _started = false;
            DestroySavedAndActualComMarker();
            DestroySavedCoLMarker();

            savedMarkers.Clear();
            isPWBFuelBalancerPart = false;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartPlaced.Remove(OnEditorPartPlaced);
                GameEvents.onEditorPartDeleted.Remove(OnEditorPartDeleted);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onHideUI.Remove(OnHideUI);
                GameEvents.onShowUI.Remove(OnShowUI);
            }
        }

        public override string GetInfo()
        {
            string str = "     Status: " + balanceStatus.ToString();
            return str;
        }

        /// <summary>
        /// Looks at all part modules on vessel of type ModulePWDFuelBalancer to find the active one
        /// if not found, then this is the first and set as the active
        /// if found, then add this one to the slaveModules list
        /// </summary>
        void FindAllPWBModules()
        {
            if (part == null || vessel == null)
                return;
            var modules = vessel.FindPartModulesImplementing<ModulePWBFuelBalancer>();
            foreach (var m in modules)
                pwbModules.Add(this);
        }

        void OnEditorPartPlaced(Part p)
        {
            if (p == this.part)
                // Set the CoM to the current CoM
                SetCoMTarget();
            FindAllPWBModules();
        }
        void OnEditorPartDeleted(Part p)
        {
            if (p != this.part)
                // Set the CoM to the current CoM
                SetCoMTarget();
            FindAllPWBModules();
        }

        private void OnHideUI() { HideUI = true; }

        private void OnShowUI() { HideUI = false; }


        /// <summary>
        /// Per-physx-frame update
        /// </summary>
        private void FixedUpdate()
        {
            Log.Info("FixedUpdate, ActualCoMMarker: " + (ActualCoMMarker != null) + ", ActualCoMMarkerVisible: " + ActualCoMMarkerVisible);
            Log.Info("FixedUpdate, ActualCoLMarker: " + (ActualCoLMarker != null) + ", ActualCoLMarkerVisible: " + ActualCoLMarkerVisible);

            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && ActualCoMMarkerVisible && !HideUI);
            if (ActualCoLMarker != null) ActualCoLMarker.SetActive(!MapView.MapIsEnabled && ActualCoLMarkerVisible && vessel.srf_velocity.magnitude > 0.1f && !HideUI);
            if (!_started || !HighLogic.LoadedSceneIsFlight || balanceStatus < BalanceStatus.Maintaining) return;

            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && SavedCoMMarkerVisible && !HideUI);

            // Update the ComError (hopefully this will not kill our performance)
            FComError = CalculateCoMFromTargetCoM(part.vessel.CoM);

            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible) return;
            if (FComError < 0.002)
            {
                // The error is so small we need not worry anymore
                if (balanceStatus == BalanceStatus.Balancing)
                {
                    DeactivateModule(this);

                    // Clear the list of tanks. They will have to be rebuilt next time balancing is enabled
                    _tanks = null;
                }
                else if (balanceStatus == BalanceStatus.Maintaining)
                {
                    // Move from a maintaining state to a standby one. If the error increases we con mvoe back to a maintining state
                    balanceStatus = BalanceStatus.Standby;
                    Status = "Standby";

                    _iNextSourceTank = 0;
                    _iNextDestinationTank = 0;
                    ResetfNextAmountMoved();

                    _fMostMovedThisRound = 0;
                }
            }
            else
            {
                // There is an error
                if (balanceStatus == BalanceStatus.Standby)
                {
                    // is the error large enough to get us back into a maintaining mode?
                    if (FComError > 0.002 * 2)
                    {
                        balanceStatus = BalanceStatus.Maintaining;
                        Status = "Maintaining";
                    }
                }
                MoveFuel();
            }
        }

        private float CalculateCoMFromTargetCoM(Vector3 vecWorldCoM)
        {
            Vector3 vecTargetComRotated = (transform.rotation * Quaternion.Inverse(RotationInEditor)) * VecFuelBalancerCoMTarget;
            Vector3 vecTargetPositionInWorldSpace = part.transform.position + vecTargetComRotated;

            float distanceFromCoMToTarget = (vecTargetPositionInWorldSpace - vecWorldCoM).magnitude;

            Log.Info("Distance between the CoM location and the target CoM location: " + distanceFromCoMToTarget.ToString());

            return (distanceFromCoMToTarget);
        }

        public void OnMouseOver()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            if (part.isAttached && Input.GetKey(SetMassKey))
            {
                if (DateTime.Now <= _lastKeyInputTime.AddMilliseconds(100)) return;
                _lastKeyInputTime = DateTime.Now;
                SetCoM();
            }
            else if (part.isAttached && Input.GetKey(DisplayMarker))
            {
                if (DateTime.Now <= _lastKeyInputTime.AddMilliseconds(100)) return;
                _lastKeyInputTime = DateTime.Now;
                ToggleAllMarkers();
            }
        }

        [KSPEvent(guiActive = true, guiName = "Toggle All Markers", active = true)]
        public void ToggleAllMarkers()
        {
            MarkerVisible = !MarkerVisible;

            SavedCoMMarkerVisible = MarkerVisible;
            ActualCoMMarkerVisible = MarkerVisible;
            ActualCoLMarkerVisible = MarkerVisible;


            // If we are in mapview then hide the marker
            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && MarkerVisible);
            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && MarkerVisible);
            if (ActualCoLMarker != null) ActualCoLMarker.SetActive(!MapView.MapIsEnabled && MarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled &&
                    (ActualCoMMarkerVisible || SavedCoMMarkerVisible || ActualCoLMarkerVisible);
        }


        void ToggleSavedMarker(bool forceOn = false)
        {
            SavedCoMMarkerVisible = !SavedCoMMarkerVisible || forceOn;
            DoCommonToggleSavedMarker();

        }

        [KSPEvent(guiActive = true, guiName = "Toggle Saved CoM Marker on", active = true)]
        public void ToggleSavedMarker()
        {
            SavedCoMMarkerVisible = !SavedCoMMarkerVisible;
            DoCommonToggleSavedMarker();
        }
        void DoCommonToggleSavedMarker()
        {
            Events["ToggleSavedMarker"].guiName = (!SavedCoMMarkerVisible ? "Toggle Saved CoM Marker on" : "Toggle Saved CoM Marker off");

            // If we are in mapview then hide the marker
            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && SavedCoMMarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled &&
                    (ActualCoMMarkerVisible || SavedCoMMarkerVisible || ActualCoLMarkerVisible);
        }

        [KSPEvent(guiActive = true, guiName = "Toggle Actual CoM Marker on", active = true)]
        public void ToggleActualMarker()
        {
            ActualCoMMarkerVisible = !ActualCoMMarkerVisible;
            if (ActualCoMMarkerVisible)
                Events["ToggleActualMarker"].guiName = "Toggle Actual CoM Marker off";
            else
                Events["ToggleActualMarker"].guiName = "Toggle Actual CoM Marker on";

            // If we are in mapview then hide the marker
            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && ActualCoMMarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled &&
                    (ActualCoMMarkerVisible || SavedCoMMarkerVisible || ActualCoLMarkerVisible);
        }

        [KSPEvent(guiActive = true, guiName = "Toggle CoL Marker on", active = true)]
        public void ToggleCoLMarker()
        {
            ActualCoLMarkerVisible = !ActualCoLMarkerVisible;
            if (ActualCoLMarkerVisible)
                Events["ToggleCoLMarker"].guiName = "Toggle CoL Marker off";
            else
                Events["ToggleCoLMarker"].guiName = "Toggle CoL Marker on";

            // If we are in mapview then hide the marker
            if (ActualCoLMarker != null) ActualCoLMarker.SetActive(!MapView.MapIsEnabled && ActualCoLMarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled &&
                    (ActualCoMMarkerVisible || SavedCoMMarkerVisible || ActualCoLMarkerVisible);
        }


        private bool SetCoMTarget()
        {
            if (part.transform == null)
                return false;

            // if (HighLogic.LoadedSceneIsEditor)
            {
                Log.Info("Part position: " + part.transform.position);
                Vector3 vecPartLocation = part.transform.position;
                Vector3 vecCom;
                if (HighLogic.LoadedSceneIsEditor)
                    vecCom = EditorMarker_CoM.findCenterOfMass(EditorLogic.RootPart);
                else
                {
                    var b = vessel.CoM - part.transform.position;
                    vecCom = Quaternion.Inverse(part.transform.rotation * Quaternion.Inverse(RotationInEditor)) * b;
                    VecFuelBalancerCoMTarget = vecCom;

                    return true;
                }

                // What really interests us is the location of the CoM relative to the part that is the balancer 
                VecFuelBalancerCoMTarget = vecCom - vecPartLocation;

            }
            // Set up the marker if we have not already done this.
            if (null == SavedCoMMarker)
            {
                //Log.Info("Setting up the CoM marker - this should have happened on Startup!");
                CreateMarkers("SetCoMTarget, part: " + part.partInfo.title);
            }
            //RotationInEditor = tmpRotation;
            return true;
        }


        internal void CreateMarkers(string msg)
        {
            // Do not try to create the marker if it already exisits
            if (SavedCoMMarker != null) return;
            Log.Info("CreateMarkers, msg: " + msg + ", MarkerVisible: " + MarkerVisible);

            // First try to find the camera that will be used to display the marker - it needs a special camera to make it "float"
            Camera markerCam = InFlightMarkerCam.GetMarkerCam();

            if (markerCam == null)
            {
                Log.Error("markerCam is null");
                return;
            }

            // Camera found, now set up the marker object, and display it via tha camera we found

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                markerCam.enabled = MarkerVisible;

            int layer = (int)(Math.Log(markerCam.cullingMask) / Math.Log(2));
            // Log.Info("MarkerCam has cullingMask: " + markerCam.cullingMask + " setting marker to be in layer: " + layer);

            ////////////// VVVVV Create Saved CoMMarker VVVVV ///////////////////////////////////////////////////////

            // Try to create a game object using our marker mesh
            SavedCoMMarker = Instantiate(GameDatabase.Instance.GetModel("PWBFuelBalancerRestored/Assets/PWBTargetComMarker"));

            // Make it a bit smaller - we need to fix the model for this
            SavedCoMMarker.transform.localScale = Vector3.one * 0.45f;

            // Add a behaviour to it to allow us to control it and link it to the part that is marks the saved CoM position for
            SavedCoMMarker savedCoMMarkerComponent = SavedCoMMarker.AddComponent<SavedCoMMarker>();
            // Tell the marker which instance of the PWBFueldBalancingModule it is displaying the set CoM location for (we could have more than one per vessel)
            savedCoMMarkerComponent.LinkPart = this;

            // Start the marker visible if it has been set to be visible, or hidden if it is set to be hidden
            SavedCoMMarker.SetActive(MarkerVisible);
            SavedCoMMarker.layer = layer;

            ////////////// ^^^^^ Create Saved CoMMarker ^^^^^ ///////////////////////////////////////////////////////

            ////////////// VVVVV Create Actual CoMMarker VVVVV ///////////////////////////////////////////////////////

            // Do it all again to create a marker for the actual centre of mass (rather than the target) TODO find a way of refactoring this
            if (!HighLogic.LoadedSceneIsFlight) return;
            // Try to create a game object using our marker mesh
            ActualCoMMarker = Instantiate(GameDatabase.Instance.GetModel("PWBFuelBalancerRestored/Assets/PWBComMarker"));

            // Make it a bit smaller - we need to fix the model for this
            ActualCoMMarker.transform.localScale = Vector3.one * 0.45f;
            ActualCoMMarker.GetComponent<Renderer>().material.color = Color.yellow;

            // Add a behaviour to it to allow us to control it and link it to the part that is marks the saved CoM position for
            PwbCoMMarker actualCoMMarkerComponent = ActualCoMMarker.AddComponent<PwbCoMMarker>();
            // Tell the marker which instance of the PWBFueldBalancingModule it is displaying the set CoM location for (we could have more than one per vessel)
            actualCoMMarkerComponent.LinkPart = this;

            // Start the marker visible if it has been set to be visible, or hidden if it is set to be hidden
            ActualCoMMarker.SetActive(MarkerVisible);

            //Log.Info("MarkerCam has cullingMask: " + markerCam.cullingMask + " setting marker to be in layer: " + layer);
            ActualCoMMarker.layer = layer;

            ////////////// ^^^^^ Create Actual CoMMarker ^^^^^ ///////////////////////////////////////////////////////

            ////////////// VVVVV Create Actual CoLMarker VVVVV ///////////////////////////////////////////////////////

            // Do it all again to create a marker for the centre of lift  TODO find a way of refactoring this
            // Try to create a game object using our marker mesh
            ActualCoLMarker = Instantiate(GameDatabase.Instance.GetModel("PWBFuelBalancerRestored/Assets/blue/PWBCoLMarker"));

            // Make it a bit smaller - we need to fix the model for this
            ActualCoLMarker.transform.localScale = Vector3.one * 0.45f;
            ActualCoLMarker.GetComponent<Renderer>().material.color = Color.blue;

            // Add a behaviour to it to allow us to control it and link it to the part that is marks the saved CoM position for
            PwbCoLMarker actualCoLMarkerComponent = ActualCoLMarker.AddComponent<PwbCoLMarker>();
            // Tell the marker which instance of the PWBFueldBalancingModule it is displaying the set CoM location for (we could have more than one per vessel)
            actualCoLMarkerComponent.LinkPart = this;

            // Start the marker visible if it has been set to be visible, or hidden if it is set to be hidden
            ActualCoLMarker.SetActive(MarkerVisible);

            //Log.Info("MarkerCam has cullingMask: " + markerCam.cullingMask + " setting marker to be in layer: " + layer);
            ActualCoLMarker.layer = layer;
            ////////////// ^^^^^ Create Actual CoLMarker VVVVV ///////////////////////////////////////////////////////


        }

        private void DestroySavedAndActualComMarker()
        {
            // Destroy the Saved Com Marker if the part is destroyed.
            if (null != SavedCoMMarker)
            {
                SavedCoMMarker.GetComponent<SavedCoMMarker>().LinkPart = null;
                Destroy(SavedCoMMarker);
                SavedCoMMarker = null;
            }

            // Destroy the Actual Com Marker if the part is destroyed.
            if (null == ActualCoMMarker) return;
            ActualCoMMarker.GetComponent<PwbCoMMarker>().LinkPart = null;
            Destroy(ActualCoMMarker);
            ActualCoMMarker = null;
        }
        private void DestroySavedCoLMarker()
        {
            // Destroy the Actual CoL Marker if the part is destroyed.
            if (null == ActualCoLMarker) return;
            ActualCoLMarker.GetComponent<PwbCoLMarker>().LinkPart = null;
            Destroy(ActualCoLMarker);
            ActualCoLMarker = null;
        }

        // Transfer fuel to move the center of mass from current position towards target.
        // Returns the new distance the CoM was moved towards its target
        public float MoveFuel()
        {
            float fCoMStartingError = CalculateCoMFromTargetCoM(vessel.CoM);
            float mass = vessel.GetTotalMass(); // Get total mass.
            Vector3 oldWorldCoM = vessel.CoM;
            float fOldCoMError = fCoMStartingError;
            bool moveMade = false;
            int iNumberofTanks = _tanks.Count;
            Log.Info("MoveFuel, mass: " + mass.ToString("F1") + ", iNumberOfTanks: " + iNumberofTanks);

            //Log.Info("Number of tanks " + iNumberofTanks);

            // Now go through the list of parts and resources and consider making transfers
            while (_iNextSourceTank < iNumberofTanks && false == moveMade)
            {
                //Log.Info("Considering moving fuel from tank" + this.iNextSourceTank);
                PartResource resource1 = ((PartAndResource)_tanks[_iNextSourceTank]).Resource;
                Part part1 = ((PartAndResource)_tanks[_iNextSourceTank]).Part;

                // Only process nonempty tanks, and tanks that are not locked.
                if (resource1.amount > 0 && resource1.flowState &&
                    resource1.resourceName != "Ore" &&
                    resource1.resourceName != "Ablator" &&
                    resource1.resourceName != "SolidFuel")
                {
                    // Only move resources that have mass (don't move electricity!)
                    if (resource1.info.density > 0)
                    {
                        // If the two tanks are the same move on.
                        if (_iNextDestinationTank == _iNextSourceTank)
                        {
                            _iNextDestinationTank++;
                        }

                        while (_iNextDestinationTank < iNumberofTanks && false == moveMade)
                        {
                            Log.Info("Considering moving fuel from tank: " + _iNextSourceTank + " to tank: " + this._iNextDestinationTank);

                            PartResource resource2 = ((PartAndResource)_tanks[_iNextDestinationTank]).Resource;
                            Part part2 = ((PartAndResource)_tanks[_iNextDestinationTank]).Part;

                            // Check that the resources are of the same type  and the tank is not locked
                            if (resource2.resourceName == resource1.resourceName && resource2.flowState)
                            {
                                // Clamp resource quantity by the amount available in the two tanks.
                                //Log.Info("MoveFuel: _iNextSourceTank: " + _iNextSourceTank + ", _fNextAmountMoved: " + ((PartAndResource)_tanks[_iNextDestinationTank])._fNextAmountMoved + ", resource1.amount: " + resource1.amount);

                                float moveAmount = (float)Math.Min(((PartAndResource)_tanks[_iNextDestinationTank]).fNextAmountMoved, resource1.amount);
                                moveAmount = (float)Math.Max(moveAmount, -(resource1.maxAmount - resource1.amount));
                                moveAmount = (float)Math.Max(moveAmount, -resource2.amount);
                                moveAmount = (float)Math.Min(moveAmount, resource2.maxAmount - resource2.amount);

                                Log.Info("Considering moving " + moveAmount.ToString("F2"));
                                if (moveAmount > 0)
                                {
                                    // Calculate the new CoM to see if it helped:
                                    float fVesselMass = vessel.GetTotalMass();

                                    //Log.Info("part1.transform.position : " + part1.transform.position.ToString());
                                    //Log.Info("part2.transform.position : " + part2.transform.position.ToString());

                                    Vector3 newCenterOfMass = ((oldWorldCoM * fVesselMass) - (part1.transform.position * (moveAmount * resource1.info.density)) + (part2.transform.position * (moveAmount * resource2.info.density))) * (1 / fVesselMass);

                                    // Recompute the distance between CoM and the TargetCoM
                                    float fNewError = CalculateCoMFromTargetCoM(newCenterOfMass);

                                    //Log.Info("Old world CoM: " + OldWorldCoM.ToString());
                                    //Log.Info("New suggested CoM: " + NewCenterOfMass.ToString());
                                    //Log.Info("new error is: " + fNewError.ToString() + " compared to " + fOldCoMError.ToString());
                                    if (fNewError < fOldCoMError)
                                    {
                                        //Log.Info("CoM moved in correct direction");
                                        // This is moving us in the right direction
                                        fOldCoMError = fNewError;

                                        // Actually move the fuel
                                        resource1.amount -= moveAmount;
                                        resource2.amount += moveAmount;

                                        // set the new CoM for the next iteration
                                        oldWorldCoM = newCenterOfMass;
                                        moveMade = true;
                                        if (moveAmount > _fMostMovedThisRound)
                                        {
                                            _fMostMovedThisRound = moveAmount;
                                        }
                                        // Finally, move on to another source tank, so that the flow out of each source tank appears a bit smoother.
                                        _iNextSourceTank++;
                                    }
                                }
                            }

                            // Move on the the next destination tank
                            _iNextDestinationTank++;
                            if (_iNextDestinationTank == _iNextSourceTank)
                            {
                                _iNextDestinationTank++;
                            }
                        }

                        // If we have reached the end of the list of destination tanks then we need to reset tank list for next time
                        if (_iNextDestinationTank < iNumberofTanks) continue;
                        _iNextDestinationTank = 0;
                        _iNextSourceTank++;
                    }
                    else
                    {
                        //Log.Info("Tank" + this.iNextSourceTank + " contains a zero density resource, moving on to the next source tank");
                        _iNextSourceTank++;
                    }
                }
                else
                {
                    //Log.Info("Tank" + this.iNextSourceTank + " was empty, moving on to the next source tank");
                    _iNextSourceTank++;
                }
            }

            // If we have reached the end of the source tanks then we need to reset the list for next time
            if (_iNextSourceTank >= iNumberofTanks)
            {
                _iNextSourceTank = 0;
                // Since we are now starting a new round, the next thing to consider is whether we moved anything this round. If not then we need to consider moving smaller amounts
                if (_fMostMovedThisRound == 0)
                {
                    ((PartAndResource)_tanks[_iNextDestinationTank]).fNextAmountMoved /= 2f;


                    //Log.Info("changing the amount to move to be " + fNextAmountMoved);

                    // Finally has the amount move become so small that we need to call it a day?
                    if (((PartAndResource)_tanks[_iNextDestinationTank]).fNextAmountMoved < 0.0005)
                    {
                        // Since perfect balance is not possible, we need to move into an appropriate state.If we are trying to maintain blanace then we will keep trying trying again with larger amounts. If we were trying for a single balance then move to a state that shows it is not possible.
                        if (balanceStatus == BalanceStatus.Maintaining)
                        {

                            ResetfNextAmountMoved();
                            Events["Disable"].active = true;
                        }
                        else
                        {
                            balanceStatus = BalanceStatus.Balance_not_possible;
                            Status = "Balance not possible";
                            Events["Disable"].active = true;
                            Events["BalanceFuel"].active = true;
                            Events["Maintain"].active = true;
                            // throw away the tanks list
                            _tanks = null;
                        }
                    }
                }
                _fMostMovedThisRound = 0;
            }

            // Update the member variable that remembers what the error is to display it
            FComError = fOldCoMError;

            // Return the amount that the CoM has been corrected
            return fCoMStartingError - fOldCoMError;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            var nodes = node.GetNodes("Marker");
            savedMarkers.Clear();
            foreach (var n in nodes)
            {
                string name = "";
                Vector3 v = new Vector3(0, 0, 0);
                n.TryGetValue("name", ref name);
                n.TryGetValue("x", ref v.x);
                n.TryGetValue("y", ref v.y);
                n.TryGetValue("z", ref v.z);
                if (name.Length > 0)
                    savedMarkers.Add(new SavedMarker(name, v));
            }
        }

        public override void OnSave(ConfigNode Cnode)
        {
            foreach (var sm in savedMarkers)
            {
                if (name.Trim().Length > 0)
                {
                    ConfigNode node = new ConfigNode();
                    node.AddValue("name", sm.name);
                    node.AddValue("x", sm.marker.x);
                    node.AddValue("y", sm.marker.y);
                    node.AddValue("z", sm.marker.z);

                    Cnode.AddNode("Marker", node);
                }
            }
        }
    }
}