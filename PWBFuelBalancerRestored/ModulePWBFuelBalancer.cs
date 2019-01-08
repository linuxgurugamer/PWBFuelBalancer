﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PWBFuelBalancer
{
    public class ModulePWBFuelBalancer : PartModule
    {
        private ArrayList _tanks;
        private int _iNextSourceTank;
        private int _iNextDestinationTank;
        private float _fNextAmountMoved;
        private float _fMostMovedThisRound;
        private float _fStartingMoveAmount;
        private Osd _osd;
        public GameObject SavedCoMMarker;
        public GameObject ActualCoMMarker;
        public bool MarkerVisible;
        public bool SavedCoMMarkerVisible;
        public bool ActualCoMMarkerVisible;

        private bool _started; // used to tell if we are set up and good to go. The Update method will check this know if it is a good idea to try to go anything or not.
        private DateTime _lastKeyInputTime;

        [KSPField]
        public string SetMassKey = "m";
        [KSPField]
        public string DisplayMarker = "d";

        [KSPField(isPersistant = true)]
        public Vector3 VecFuelBalancerCoMTarget;

        [KSPField(isPersistant = true)]
        public string BalancerName = "PWBFuelBalancer";

        [KSPField(isPersistant = true)]
        public string Save1Name = "Save1";

        [KSPField(isPersistant = true)]
        public Vector3 VecSave1CoMTarget;

        [KSPField(isPersistant = true)]
        public string Save2Name = "Save2";

        [KSPField(isPersistant = true)]
        public Vector3 VecSave2CoMTarget;

        [KSPField(isPersistant = true)]
        public Quaternion RotationInEditor;

        enum BalanceStatus  {Deactivated, Maintaining, Balancing, Standby, Balance_not_possible };
        BalanceStatus balanceStatus;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string Status;

        [KSPField(isPersistant = false, guiActive = true, guiName = "CoM Error", guiUnits = "m", guiFormat = "f3")]
        public float FComError;

        [KSPAction("Balance Fuel Tanks")]
        public void BalanceFuelAction(KSPActionParam param)
        {
            BalanceFuel();
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate", active = false)]
        public void Disable()
        {
            balanceStatus = BalanceStatus.Deactivated;
            Status = "Deactivated";
            Events["Disable"].active = false;
            Events["BalanceFuel"].active = true;
            Events["Maintain"].active = true;

            // Clear the list of tanks. They will have to be rebuilt next time balancing is enabled
            _tanks = null;
        }

        [KSPEvent(guiActive = true, guiName = "Keep Balanced", active = true)]
        public void Maintain()
        {
            // If we were previously Deactivated then we need to build a list of tanks and set up to start balancing
            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible)
            {
                BuildTanksList();

                _iNextSourceTank = 0;
                _iNextDestinationTank = 0;
                _fNextAmountMoved = _fStartingMoveAmount;
                _fMostMovedThisRound = 0;
            }

            Events["Disable"].active = true;
            Events["BalanceFuel"].active = false;
            Events["Maintain"].active = false;
            balanceStatus = BalanceStatus.Maintaining;
            Status = "Maintaining";
        }

        [KSPEvent(guiActiveEditor = true)]
        void SetCoM()
        {
            if (SetCoMTarget())
                ScreenMessages.PostScreenMessage("CoM Target Set", 5);
        }

        [KSPEvent(guiActive = true, guiName = "Balance Fuel", active = true)]
        public void BalanceFuel()
        {
            // If we were previousyl deactive, then we need to build the loist of tanks and set up to start balancing
            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible)
            {
                BuildTanksList();

                _iNextSourceTank = 0;
                _iNextDestinationTank = 0;
                _fNextAmountMoved = _fStartingMoveAmount;
                _fMostMovedThisRound = 0;
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
            //print("building a tank list");

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
                    if (((PartResource)resources.Current).info.density > 0 )
                    { // Only consider resources that have mass (don't move electricity!)
                        _tanks.Add(new PartAndResource(parts.Current, (PartResource)resources.Current));
                    }
                }
            }
        }

        public void OnDestroy()
        {
            _started = false;
            DestroySavedComMarker();
        }



        /// <summary>
        /// Constructor style setup.
        /// Called in the Part\'s Awake method. 
        /// The model may not be built by this point.
        /// </summary>
        public override void OnAwake()
        {
            _tanks = null;
            MarkerVisible = false;
            SavedCoMMarkerVisible = false;
            ActualCoMMarkerVisible = false;
    }

        /// <summary>
        /// Called during the Part startup.
        /// StartState gives flag values of initial state
        /// </summary>
        public override void OnStart(StartState state)
        {
            // Set the status to be deactivated
            balanceStatus = BalanceStatus.Deactivated;
            Status = "Deactivated";
            _osd = new Osd();
            _fStartingMoveAmount = 1; // TODO change this to reflect flow rates and the physics frame rate
            SavedCoMMarker = null; // marker to display the saved location

            _lastKeyInputTime = DateTime.Now;

            CreateSavedComMarker();

            _started = true;
            
            GameEvents.onEditorPartPlaced.Add(OnEditorPartPlaced);
            
        }

        public  void Destroy()
        {
            GameEvents.onEditorPartPlaced.Remove(OnEditorPartPlaced);
        }

        void OnEditorPartPlaced(Part p)
        {
            if (p == this.part)
                // Set the CoM to the current CoM
                SetCoMTarget();
        }

        /// <summary>
        /// Per-frame update
        /// Called ONLY when Part is ACTIVE!
        /// </summary>
        public override void OnUpdate()
        {
        }

        /// <summary>
        /// Per-physx-frame update
        /// </summary>
        private void FixedUpdate()
        {
            if (!_started) return;
            // Only do this while in flight
            if (!HighLogic.LoadedSceneIsFlight) return;
            // Update the ComError (hopefully this will not kill our performance)
            FComError = CalculateCoMFromTargetCoM(part.vessel.CoM);

            if (balanceStatus == BalanceStatus.Deactivated || balanceStatus == BalanceStatus.Balance_not_possible) return;
            if (FComError < 0.002)
            {
                // The error is so small we need not worry anymore
                if (balanceStatus == BalanceStatus.Balancing)
                {
                    balanceStatus = BalanceStatus.Deactivated;
                    Status = "Deactivated";
                    Events["Disable"].active = false;
                    Events["BalanceFuel"].active = true;
                    Events["Maintain"].active = true;

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
                    _fNextAmountMoved = _fStartingMoveAmount;
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

            // print("Distance between the CoM location and the target CoM location: " + distanceFromCoMToTarget.ToString());

            return (distanceFromCoMToTarget);
        }

#if false
        private void DumpConfigNode(ConfigNode node)
        {
            print("ConfigNode: name: " + node.name + " id: " + node.id);
            print("values: ");
            print("ToString: " + node);
        }
#endif
        public void OnMouseOver()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            if (part.isAttached && Input.GetKey(SetMassKey))
            {
                if (DateTime.Now <= _lastKeyInputTime.AddMilliseconds(100)) return;
                _lastKeyInputTime = DateTime.Now;
                SetCoM();
                //if (SetCoMTarget())
                //    ScreenMessages.PostScreenMessage("CoM Target Set", 5);
            }
            else if (part.isAttached && Input.GetKey(DisplayMarker))
            {
                if (DateTime.Now <= _lastKeyInputTime.AddMilliseconds(100)) return;
                _lastKeyInputTime = DateTime.Now;
                ToggleMarker();
            }
        }

        [KSPEvent(guiActive = true, guiName = "Toggle All Markers", active = true)]
        public void ToggleMarker()
        {
            MarkerVisible = !MarkerVisible;

            SavedCoMMarkerVisible = !SavedCoMMarkerVisible;
            ActualCoMMarkerVisible = !ActualCoMMarkerVisible;

            // If we are in mapview then hide the marker
            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && MarkerVisible);

            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && MarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled && (ActualCoMMarkerVisible || SavedCoMMarkerVisible);


            {
#if false
                if (HighLogic.LoadedSceneIsFlight)
                {
                    Log.Info("vessel.transform.rotation : " + this.vessel.transform.rotation);
                    Log.Info("vessel.ReferenceTransform.rotation : " + this.vessel.ReferenceTransform.rotation);
                    Log.Info("vessel.transform.rotation .eulerAngles: " + this.vessel.transform.rotation.eulerAngles);
                    Log.Info("vessel.upaxis : " + this.vessel.upAxis);

                    Log.Info("upaxis: " + (Vector3)(Quaternion.Inverse(this.vessel.transform.rotation) *this.vessel.upAxis ));
                }
#endif
            }
        }

        [KSPEvent(guiActive = true, guiName = "Toggle Saved CoM Marker on", active = true)]
        public void ToggleSavedMarker()
        {
            SavedCoMMarkerVisible = !SavedCoMMarkerVisible;
            if (SavedCoMMarkerVisible)
                Events["ToggleSavedMarker"].guiName = "Toggle Saved CoM Marker off";
            else
                Events["ToggleSavedMarker"].guiName = "Toggle Saved CoM Marker on";

            // If we are in mapview then hide the marker
            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && SavedCoMMarkerVisible);

            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && ActualCoMMarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled && (ActualCoMMarkerVisible || SavedCoMMarkerVisible);
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
            if (SavedCoMMarker != null) SavedCoMMarker.SetActive(!MapView.MapIsEnabled && SavedCoMMarkerVisible);

            if (ActualCoMMarker != null) ActualCoMMarker.SetActive(!MapView.MapIsEnabled && ActualCoMMarkerVisible);

            if (InFlightMarkerCam.MarkerCam != null)
                InFlightMarkerCam.MarkerCam.enabled = !MapView.MapIsEnabled && (ActualCoMMarkerVisible || SavedCoMMarkerVisible);
        }


        private bool SetCoMTarget()
        {
            //bool initalOff = false;

            // Following code replaced by EditorMarker_CoM.findCenterOfMass(EditorLogic.RootPart);
#if false
            // We are depending on the CoM indicator for the location of the CoM which is a bit rubbish :( There ust be a better way of doing this!
            EditorMarker_CoM coM = (EditorMarker_CoM)FindObjectOfType(typeof(EditorMarker_CoM));
            if (coM == null)
            {
                EditorVesselOverlays evo = (EditorVesselOverlays)FindObjectOfType(typeof(EditorVesselOverlays));
                if (evo != null)
                {
                    evo.CoMmarker.gameObject.SetActive(true);
                    coM = evo.CoMmarker;
                    initalOff = true;
                }
                if (coM == null)
                {
                    // There is no CoM indicator. Spawn an instruction screen or something
                    _osd.Error("To set the target CoM, first turn on the CoM Marker");
                    ScreenMessages.PostScreenMessage("To set the target CoM, first turn on the CoM Marker", 5);
                    return false;
                }
            }
            //else
#endif
            {
                // get the location of the centre of mass
                //print("Com position: " + CoM.transform.position);
                //Vector3 vecCom = coM.transform.position;

                //Log.Info("vecCom: " + vecCom);
                //Log.Info("coM.findCenterOfMass: " + EditorMarker_CoM.findCenterOfMass(EditorLogic.RootPart));
                Vector3 vecCom = EditorMarker_CoM.findCenterOfMass(EditorLogic.RootPart);

                RotationInEditor = part.transform.rotation;
                //print("Part position: " + part.transform.position);
                Vector3 vecPartLocation = part.transform.position;
                //print("vecPartLocation: " + vecPartLocation);

                // What really interests us is the location fo the CoM relative to the part that is the balancer 
                VecFuelBalancerCoMTarget = vecCom - vecPartLocation;
                //print("vecFuelBalancerCoMTarget: " + this.vecFuelBalancerCoMTarget + "rotationInEditor: " + this.rotationInEditor);

                // Set up the marker if we have not already done this.
                if (null == SavedCoMMarker)
                {
                    //print("Setting up the CoM marker - this should have happened on Startup!");
                    CreateSavedComMarker();
                }

                _osd.Success("The CoM has been set");

                // TODO remove - Diagnostics
                {
                    //print("EditorLogic.VesselRotation : " + EditorLogic.VesselRotation);
                }
            }
            //print("Setting the targetCoM location for fuel balancing.");
#if false
            if (initalOff)
            {
                coM.gameObject.SetActive(false);
                ScreenMessages.PostScreenMessage("Target CoM set to current (hidden) CoM", 5);
            }
#endif
            return true;
        }


        private void CreateSavedComMarker()
        {
            if (SavedCoMMarker == null)
                Log.Info("SavedCoMMarker is null");
            
            // Do not try to create the marker if it already exisits
            if (null != SavedCoMMarker) return;
            // First try to find the camera that will be used to display the marker - it needs a special camera to make it "float"
            Camera markerCam = InFlightMarkerCam.GetMarkerCam();

            if (markerCam == null)
                Log.Info("markerCam is null");
            // Did we find the camera? If we did then set up the marker object, and display it via tha camera we found
            if (null == markerCam) return;
            // Try to create a game object using our marker mesh
            SavedCoMMarker = Instantiate(GameDatabase.Instance.GetModel("PWBFuelBalancerRestored/Assets/PWBTargetComMarker"));

            // Make it a bit smaller - we need to fix the model for this
            SavedCoMMarker.transform.localScale = Vector3.one * 0.5f;

            // Add a behaviour to it to allow us to control it and link it to the part that is marks the saved CoM position for
            SavedCoMMarker.AddComponent<SavedCoMMarker>();
            // Tell the marker which instance of the PWBFueldBalancingModule it is displaying the set CoM location for (we could have more than one per vessel)
            SavedCoMMarker.GetComponent<SavedCoMMarker>().LinkPart(this);

            // Start the marker visible if it has been set to be visible, or hidden if it is set to be hidden
            SavedCoMMarker.SetActive(MarkerVisible);
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                markerCam.enabled = MarkerVisible;

            int layer = (int)(Math.Log(markerCam.cullingMask) / Math.Log(2));
            // print("MarkerCam has cullingMask: " + markerCam.cullingMask + " setting marker to be in layer: " + layer);
            SavedCoMMarker.layer = layer;


            // Do it all again to create a marker for the actual centre of mass (rather than the target) TODO find a way of refactoring this
            if (!HighLogic.LoadedSceneIsFlight) return;
            // Try to create a game object using our marker mesh
            ActualCoMMarker = Instantiate(GameDatabase.Instance.GetModel("PWBFuelBalancerRestored/Assets/PWBComMarker"));

            // Make it a bit smaller - we need to fix the model for this
            ActualCoMMarker.transform.localScale = Vector3.one * 0.45f;
            ActualCoMMarker.GetComponent<Renderer>().material.color = Color.yellow;

            // Add a behaviour to it to allow us to control it and link it to the part that is marks the saved CoM position for
            ActualCoMMarker.AddComponent<PwbcoMMarker>();
            // Tell the marker which instance of the PWBFueldBalancingModule it is displaying the set CoM location for (we could have more than one per vessel)
            ActualCoMMarker.GetComponent<PwbcoMMarker>().LinkPart(this);

            // Start the marker visible if it has been set to be visible, or hidden if it is set to be hidden
            ActualCoMMarker.SetActive(MarkerVisible);

            //print("MarkerCam has cullingMask: " + markerCam.cullingMask + " setting marker to be in layer: " + layer);
            ActualCoMMarker.layer = layer;
        }

        private void DestroySavedComMarker()
        {
            // Destroy the Saved Com Marker if the part is destroyed.
            if (null != SavedCoMMarker)
            {
                SavedCoMMarker.GetComponent<SavedCoMMarker>().LinkPart(null);
                Destroy(SavedCoMMarker);
                SavedCoMMarker = null;
            }

            // Destroy the Actual Com Marker if the part is destroyed.
            if (null == ActualCoMMarker) return;
            ActualCoMMarker.GetComponent<PwbcoMMarker>().LinkPart(null);
            Destroy(ActualCoMMarker);
            ActualCoMMarker = null;
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

            //print("Number of tanks " + iNumberofTanks);

            // Now go through the list of parts and resources and consider making transfers
            while (_iNextSourceTank < iNumberofTanks && false == moveMade)
            {
                //print("Considering moveing fuel from tank" + this.iNextSourceTank);
                PartResource resource1 = ((PartAndResource)_tanks[_iNextSourceTank]).Resource;
                Part part1 = ((PartAndResource)_tanks[_iNextSourceTank]).Part;

                // Only process nonempty tanks, and tanks that are not locked.
                if (resource1.amount > 0 && resource1.flowState)
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
                            //print("Considering moving fuel to tank" + this.iNextDestinationTank);

                            PartResource resource2 = ((PartAndResource)_tanks[_iNextDestinationTank]).Resource;
                            Part part2 = ((PartAndResource)_tanks[_iNextDestinationTank]).Part;

                            // Check that the resources are of the same type  and the tank is not locked
                            if (resource2.resourceName == resource1.resourceName && resource2.flowState)
                            {
                                // Clamp resource quantity by the amount available in the two tanks.
                                float moveAmount = (float)Math.Min(_fNextAmountMoved, resource1.amount);
                                moveAmount = (float)Math.Max(moveAmount, -(resource1.maxAmount - resource1.amount));
                                moveAmount = (float)Math.Max(moveAmount, -resource2.amount);
                                moveAmount = (float)Math.Min(moveAmount, resource2.maxAmount - resource2.amount);

                                //print("considering moving " + moveAmount.ToString());
                                if (moveAmount > 0)
                                {
                                    // Calculate the new CoM to see if it helped:
                                    float fVesselMass = vessel.GetTotalMass();

                                    //print("part1.transform.position : " + part1.transform.position.ToString());
                                    //print("part2.transform.position : " + part2.transform.position.ToString());

                                    Vector3 newCenterOfMass = ((oldWorldCoM * fVesselMass) - (part1.transform.position * (moveAmount * resource1.info.density)) + (part2.transform.position * (moveAmount * resource2.info.density))) * (1 / fVesselMass);

                                    // Recompute the distance between CoM and the TargetCoM
                                    float fNewError = CalculateCoMFromTargetCoM(newCenterOfMass);

                                    //print("Old world CoM: " + OldWorldCoM.ToString());
                                    //print("New suggested CoM: " + NewCenterOfMass.ToString());
                                    //print("new error is: " + fNewError.ToString() + " compared to " + fOldCoMError.ToString());
                                    if (fNewError < fOldCoMError)
                                    {
                                        //print("CoM moved in correct direction");
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
                        //print("Tank" + this.iNextSourceTank + " contains a zero density resource, moving on to the next source tank");
                        _iNextSourceTank++;
                    }
                }
                else
                {
                    //print("Tank" + this.iNextSourceTank + " was empty, moving on to the next source tank");
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
                    _fNextAmountMoved = _fNextAmountMoved / 2f;
                    //print("changing the amount to move to be " + fNextAmountMoved);

                    // Finally has the amount move become so small that we need to call it a day?
                    if (_fNextAmountMoved < 0.0005)
                    {
                        // Since perfect balance is not possible, we need to move into an appropriate state.If we are trying to maiintain blanace then we will keep trying trying again with larger amounts. If we were trying for a single balance then move to a state that shows it is not possible.
                        if (balanceStatus == BalanceStatus.Maintaining)
                        {
                            _fNextAmountMoved = _fStartingMoveAmount;
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

        public void OnGui()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            EditorLogic editor = EditorLogic.fetch;
            if (editor == null) return;
            if (editor.editorScreen == EditorScreen.Parts)
            {
                _osd.Update();
            }
        }
    }

}
