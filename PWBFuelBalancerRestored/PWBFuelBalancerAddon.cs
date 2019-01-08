using System;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;

using ClickThroughFix;
using ToolbarControl_NS;

namespace PWBFuelBalancer
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class PwbFuelBalancerAddon : MonoBehaviour
    {
        // The Addon on keeps a reference to all the PWBFuelBalancers in the current vessel. If the current vessel changes or is modified then this list will need to be rebuilt.
        private List<ModulePWBFuelBalancer> _listFuelBalancers;

        private static Rect _windowPositionEditor = new Rect(265, 90, 400, 36);
        private static Rect _windowPositionFlight = new Rect(150, 50, 360, 36);
        private static Rect _currentWindowPosition;
        private static GUIStyle _windowStyle;
                
        ToolbarControl toolbarControl;

        private bool _visible;

        private int _editorPartCount;

        private int _selectedBalancer;

        public static PwbFuelBalancerAddon Instance
        {
            get;
            private set;
        }

        public PwbFuelBalancerAddon()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

 
        public void Awake()
        {
            // create the list of balancers
            _listFuelBalancers = new List<ModulePWBFuelBalancer>();


            InitializeToolbar();

            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselLoaded.Add(OnVesselLoaded);
            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            GameEvents.onFlightReady.Add(OnFlightReady);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
            GameEvents.onEditorLoad.Add(this.OnEditorLoad);
        }

        public void Start()
        {
            _currentWindowPosition = HighLogic.LoadedSceneIsEditor ? _windowPositionEditor : _windowPositionFlight;
            _windowStyle = new GUIStyle(HighLogic.Skin.window);

            //if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;



        }


        private void OnGameSceneLoadRequested(GameScenes scene)
        {
            //This handles scene specific window positioning.  Soon I'll add persistence...
            if (scene == GameScenes.EDITOR) _windowPositionEditor = _currentWindowPosition;
            else _windowPositionFlight = _currentWindowPosition;

            _currentWindowPosition = scene == GameScenes.EDITOR ? _windowPositionEditor : _windowPositionFlight;
        }

        internal const string MODID = "PWBFuelBalancer_NS";
        internal const string MODNAME = "PWB Fuel Balancer";

        private void InitializeToolbar()
        {
            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(OnAppLaunchToggle, OnAppLaunchToggle,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.FLIGHT,
                MODID,
                "slingShotterButton",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_on_38",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_off_38",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_on_24",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_off_24",
                MODNAME
            );

        }

        private void OnAppLaunchToggle()
        {

            _visible = !_visible;
        }


        private void DummyVoid() { }

        private void OnGUI()
        {
            if (_visible)
            {
                //Set the GUI Skin
                if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().useKSPskin)
                    GUI.skin = HighLogic.Skin;
                _currentWindowPosition = ClickThruBlocker.GUILayoutWindow(947695, _currentWindowPosition, OnWindow, "PWB Fuel Balancer", _windowStyle, GUILayout.MinHeight(20), GUILayout.MinWidth(100), GUILayout.ExpandHeight(true));
            }

            GuiUtils.ComboBox.DrawGui();
        }


        private void OnWindow(int windowId)
        {
            try
            {
                GUIStyle buttonStyle = new GUIStyle( GUI.skin.button);
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.padding.top = -4;
                buttonStyle.padding.left = 3;
 
                Rect rect = new Rect(_currentWindowPosition.width - 20, 4, 16, 16);
                if (GUI.Button(rect, "x"))
                {
                    toolbarControl.SetFalse(true);
                    //OnAppLaunchToggle();
                }
                GUILayout.BeginVertical();
                List<string> strings = new List<string>();

                if (_listFuelBalancers.Count > 0)
                {
                    List<ModulePWBFuelBalancer>.Enumerator balancers = _listFuelBalancers.GetEnumerator();
                    while (balancers.MoveNext())
                    {
                        if (balancers.Current == null) continue;
                        strings.Add(balancers.Current.BalancerName);// + " position:" + balancer.vecFuelBalancerCoMTarget.ToString());
                                                                    //              GUILayout.Label(balancer.name + " position:" + balancer.vecFuelBalancerCoMTarget.ToString());
                    }

                    _selectedBalancer = GuiUtils.ComboBox.Box(_selectedBalancer, strings.ToArray(), this);


                    // It will be useful to have a reference to the selected balancer
                    ModulePWBFuelBalancer selBal = _listFuelBalancers[_selectedBalancer];

                    // Provide a facility to change the name of the balancer
                    {
                        string oldName = selBal.BalancerName;
                        string newName = GUILayout.TextField(oldName);

                        if (oldName != newName)
                        {
                            selBal.BalancerName = newName;
                        }
                    }
                    GUILayout.BeginHorizontal();

                    GUILayout.BeginVertical();
                    if (GUILayout.Button("up"))
                    {
                        //if (selBal.vessel.vesselType == VesselType.Plane)
                        selBal.VecFuelBalancerCoMTarget.y += 0.05f;
                        //else
                        //    selBal.VecFuelBalancerCoMTarget.y -= 0.05f;
                    }
                    if (GUILayout.Button("down"))
                    {
                        //if (selBal.vessel.vesselType == VesselType.Plane)
                        selBal.VecFuelBalancerCoMTarget.y -= 0.05f;
                        //else
                        //    selBal.VecFuelBalancerCoMTarget.y += 0.05f;
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (GUILayout.Button("forward"))
                    {
                        if ((HighLogic.LoadedSceneIsEditor && EditorDriver.editorFacility == EditorFacility.SPH) ||
                                (HighLogic.LoadedSceneIsFlight && selBal.vessel.vesselType == VesselType.Plane))
                            selBal.VecFuelBalancerCoMTarget.z += 0.05f;
                        else
                            selBal.VecFuelBalancerCoMTarget.x += 0.05f;
                    }

                    if (GUILayout.Button("back"))
                    {
                        if ((HighLogic.LoadedSceneIsEditor && EditorDriver.editorFacility == EditorFacility.SPH) ||
                            (HighLogic.LoadedSceneIsFlight && selBal.vessel.vesselType == VesselType.Plane))
                            selBal.VecFuelBalancerCoMTarget.z -= 0.05f;
                        else
                            selBal.VecFuelBalancerCoMTarget.x -= 0.05f;
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("left"))
                    {
                        if ((HighLogic.LoadedSceneIsEditor && EditorDriver.editorFacility == EditorFacility.SPH) ||
                            (HighLogic.LoadedSceneIsFlight && selBal.vessel.vesselType == VesselType.Plane))
                            selBal.VecFuelBalancerCoMTarget.x -= 0.05f;
                        else
                            selBal.VecFuelBalancerCoMTarget.z += 0.05f;
                    }
                    if (GUILayout.Button("right"))
                    {
                        if ((HighLogic.LoadedSceneIsEditor && EditorDriver.editorFacility == EditorFacility.SPH) ||
                            (HighLogic.LoadedSceneIsFlight && selBal.vessel.vesselType == VesselType.Plane))
                            selBal.VecFuelBalancerCoMTarget.x += 0.05f;
                        else
                            selBal.VecFuelBalancerCoMTarget.z -= 0.05f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    string toggleText = selBal.MarkerVisible ? "Hide Saved CoM" : "Show Saved CoM";

                    if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                    {
                        selBal.ToggleSavedMarker();
                    }

                    GUILayout.FlexibleSpace();

                    if (HighLogic.LoadedSceneIsFlight)
                    {
                       
                        toggleText = selBal.MarkerVisible ? "Hide Markers" : "Show Markers";

                        if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                        {
                            selBal.ToggleMarker();
                        }

                        GUILayout.FlexibleSpace();

                       toggleText = selBal.MarkerVisible ? "Hide Actual CoM" : "Show Actual CoM";

                        if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                        {
                            selBal.ToggleActualMarker();
                        }
                        GUILayout.FlexibleSpace();

                    }

                    GUILayout.EndHorizontal();

                    // Save slot 1
                    GUILayout.BeginHorizontal();

                    selBal.Save1Name = GUILayout.TextField(selBal.Save1Name);

                    if (GUILayout.Button("Load"))
                    {
                        selBal.VecFuelBalancerCoMTarget = selBal.VecSave1CoMTarget;
                    }

                    if (GUILayout.Button("Save"))
                    {
                        selBal.VecSave1CoMTarget = selBal.VecFuelBalancerCoMTarget;
                    }
                    GUILayout.EndHorizontal();

                    // Save slot 2
                    GUILayout.BeginHorizontal();
                    selBal.Save2Name = GUILayout.TextField(selBal.Save2Name);

                    if (GUILayout.Button("Load"))
                    {
                        selBal.VecFuelBalancerCoMTarget = selBal.VecSave2CoMTarget;
                    }

                    if (GUILayout.Button("Save"))
                    {
                        selBal.VecSave2CoMTarget = selBal.VecFuelBalancerCoMTarget;
                    }

                    GUILayout.EndHorizontal();

                }
                else
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("No Fuel Balancers mounted");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndVertical();
                GUI.DragWindow();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Update()
        {
            // Debug.Log("PWBFuelBalancerAddon:Update");
        }

        public void FixedUpdate()
        {
            try
            {
                // Debug.Log("PWBFuelBalancerAddon:FixedUpdate");

                // With the new onEditorShipModified event, this is no longer necessary.

                // If we are in the editor, and there is a ship in the editor, then compare the number of parts to last time we did this. If it has changed then rebuild the CLSVessel
                //if (!HighLogic.LoadedSceneIsEditor) return;
                //int currentPartCount = 0;
                //currentPartCount = null == EditorLogic.RootPart ? 0 : EditorLogic.SortedShipList.Count;

                //if (currentPartCount == _editorPartCount) return;
                ////Debug.Log("Calling RebuildCLSVessel as the part count has changed in the editor");
                //BuildBalancerList();

                //_editorPartCount = currentPartCount;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            GameEvents.onFlightReady.Remove(OnFlightReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);
            GameEvents.onEditorLoad.Remove(this.OnEditorLoad);


            // Remove the stock toolbar button
            GameEvents.onGUIApplicationLauncherReady.Remove(InitializeToolbar);
            //if (_stockToolbarButton != null)
            //    ApplicationLauncher.Instance.RemoveModApplication(_stockToolbarButton);
            if (toolbarControl != null)
            {
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
            }
        }

        private void BuildBalancerList()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                BuildBalancerList(FlightGlobals.ActiveVessel.parts);
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                // If there is no root part in the editor - this ought to mean that there are no parts. Just clear out everything
                if (null == EditorLogic.RootPart)
                {
                    _listFuelBalancers.Clear();
                }
                else
                {
                    BuildBalancerList(EditorLogic.RootPart.vessel.parts);
                }
            }
        }

        // Builds a list of off the ModulePWBFuelBalancers in the whole of the current vessel.
        private void BuildBalancerList(Vessel v)
        {
            BuildBalancerList(v.parts);
        }

        private void BuildBalancerList(List<Part> partList)
        {
            _listFuelBalancers.Clear();
            _listFuelBalancers = GetBalancers(partList);
        }

        internal static List<ModulePWBFuelBalancer> GetBalancers(List<Part> partList)
        {
            List<ModulePWBFuelBalancer> modList = new List<ModulePWBFuelBalancer>();
            List<Part>.Enumerator iParts = partList.GetEnumerator();
            while (iParts.MoveNext())
            {
                if (iParts.Current == null) continue;
                if (iParts.Current.Modules.Contains<ModulePWBFuelBalancer>())
                {
                    modList.AddRange(iParts.Current.Modules.GetModules<ModulePWBFuelBalancer>());
                }
            }
            return modList;
        }

        // This event is fired when the vessel is changed. If this happens we need to rebuild the list of balancers in the vessel.
        private void OnVesselChange(Vessel data)
        {
            BuildBalancerList(data);
        }

        private void OnVesselWasModified(Vessel data)
        {
            BuildBalancerList(data);
        }

        private void OnFlightReady()
        {
            // Now build the list of balancers
            BuildBalancerList();
        }

        private void OnVesselLoaded(Vessel data)
        {
            BuildBalancerList();
        }
        private void OnEditorShipModified(ShipConstruct vesselConstruct)
        {
            if (vesselConstruct.Parts.Count == _editorPartCount) return;
            BuildBalancerList(vesselConstruct.Parts);
            _editorPartCount = vesselConstruct.parts.Count;
        }
        private void OnEditorLoad(ShipConstruct vesselConstruct, CraftBrowserDialog.LoadType loadType)
        {
            BuildBalancerList(vesselConstruct.Parts);
            _editorPartCount = vesselConstruct.parts.Count;
        }
    }
}