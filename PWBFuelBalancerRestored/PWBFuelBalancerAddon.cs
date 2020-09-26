using System;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;
using KSP_Log;

using ClickThroughFix;
using ToolbarControl_NS;
using System.Linq;

namespace PWBFuelBalancer
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class PwbFuelBalancerAddon : MonoBehaviour
    {
        internal static PwbFuelBalancerAddon fetch;
        // The Addon on keeps a reference to all the PWBFuelBalancers in the current vessel. If the current vessel changes or is modified then this list will need to be rebuilt.
        private List<ModulePWBFuelBalancer> _listFuelBalancers;

        internal static Log Log = new Log("PWBFuelBalancer.PwbFuelBalancerAddon");

        private static Rect _windowPositionEditor = new Rect(265, 90, 100, 100);
        private static Rect _windowPositionFlight = new Rect(150, 50, 100, 100);
        private static Rect _currentWindowPosition;
        private static GUIStyle _windowStyle;

        ToolbarControl toolbarControl;

        private bool visible;

        private int editorPartCount;

        private int selectedBalancer;
        private int oldSelectedBalancer = -1;
        private int activeSavedCoM = -1;

        internal bool HideUI { get; set; }

        #region GUIStyles
        private static GUIStyle yellowTextField;
        private static GUIStyle normalTextField;
        public static GUIStyle YellowTextField
        {
            get
            {
                if (yellowTextField != null) return yellowTextField;
                normalTextField = new GUIStyle(GUI.skin.textField);
                Texture2D t = new Texture2D(1, 1);
                t.SetPixel(0, 0, new Color(0, 0, 0, 0));
                t.Apply();
                yellowTextField = new GUIStyle(GUI.skin.textField)
                {
                    normal =
                    { textColor = Color.yellow },
                    focused =
                    { textColor = Color.yellow },
                    fontStyle = FontStyle.Bold
                };
                return yellowTextField;
            }
        }
        public static GUIStyle NormalTextField
        {
            get
            {
                if (normalTextField != null) return normalTextField;
                normalTextField = new GUIStyle(GUI.skin.textField);
                return normalTextField;
            }
        }

        private static GUIStyle yellowLabel;
        private static GUIStyle normalLabel;
        public static GUIStyle YellowLabel
        {
            get
            {
                if (yellowLabel != null) return yellowLabel;
                normalLabel = new GUIStyle(GUI.skin.label);
                Texture2D t = new Texture2D(1, 1);
                t.SetPixel(0, 0, new Color(0, 0, 0, 0));
                t.Apply();
                yellowLabel = new GUIStyle(GUI.skin.label)
                {
                    normal =
                    { textColor = Color.yellow },
                    focused =
                    { textColor = Color.yellow },
                    fontStyle = FontStyle.Bold
                };
                return yellowLabel;
            }
        }
        public static GUIStyle NormalLabel
        {
            get
            {
                if (normalLabel != null) return normalLabel;
                normalLabel = new GUIStyle(GUI.skin.label);
                return normalLabel;
            }
        }

        private static GUIStyle yellowButton;
        private static GUIStyle normalButton;
        private static GUIStyle whiteButton;
        public static GUIStyle YellowButton
        {
            get
            {
                if (yellowButton != null) return yellowButton;
                normalButton = new GUIStyle(GUI.skin.button);
                Texture2D t = new Texture2D(1, 1);
                t.SetPixel(0, 0, new Color(0, 0, 0, 0));
                t.Apply();
                yellowButton = new GUIStyle(GUI.skin.button)
                {
                    normal =
                    { textColor = Color.yellow },
                    focused =
                    { textColor = Color.yellow },
                    fontStyle = FontStyle.Bold
                };
                return yellowButton;
            }
        }
        public static GUIStyle NormalButton
        {
            get
            {
                if (normalButton != null) return normalButton;
                normalButton = new GUIStyle(GUI.skin.button);
                return normalButton;
            }
        }

        public static GUIStyle WhiteButton
        {
            get
            {
                if (whiteButton != null) return whiteButton;
                whiteButton = new GUIStyle(GUI.skin.button);
                whiteButton.normal.textColor = Color.white;
                whiteButton.fontStyle = FontStyle.Bold;
                whiteButton.alignment = TextAnchor.MiddleCenter;
                whiteButton.padding.top = 0;
                whiteButton.padding.bottom = 0;
                whiteButton.padding.left = 0;
                whiteButton.padding.right = 0;
                return whiteButton;
            }
        }
        #endregion

        public void Awake()
        {
            fetch = this;
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
            GameEvents.onEditorNewShipDialogDismiss.Add(this.OnEditorNewShipDialogDismiss);
            GameEvents.onPartExplode.Add(OnPartExplode);
            GameEvents.onPartExplodeGroundCollision.Add(OnPartExplodeGroundCollision);
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onHideUI.Add(OnHideUI);
                GameEvents.onShowUI.Add(OnShowUI);
            }
        }

        private void OnHideUI() { HideUI = true; }

        private void OnShowUI() { HideUI = false; }


        public void Start()
        {
            _currentWindowPosition = HighLogic.LoadedSceneIsEditor ? _windowPositionEditor : _windowPositionFlight;
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            BuildBalancerList(FlightGlobals.ActiveVessel);
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
                "PWBFuelBalancerButton",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_on_38",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_off_38",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_on_24",
                "PWBFuelBalancerRestored/PluginData/Assets/pwbfuelbalancer_icon_off_24",
                MODNAME
            );
        }

        internal void OnAppLaunchToggle()
        {
            visible = !visible;
        }
        enum EditMode  {none = -1, compact = 0, select = 1, full = 2 };
        EditMode oldEditMode = EditMode.none;
        EditMode editMode = EditMode.compact;

        private void OnGUI()
        {
            if (visible && !HideUI)
            {
                if (editMode != oldEditMode)
                {
                    oldEditMode = editMode;
                    _currentWindowPosition.width = 100;
                    _currentWindowPosition.height = 100;
                }
                //Set the GUI Skin
                if (HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().useKSPskin)
                    GUI.skin = HighLogic.Skin;
                _currentWindowPosition = ClickThruBlocker.GUILayoutWindow(947695, _currentWindowPosition, OnWindow, "PWB Fuel Balancer", _windowStyle, GUILayout.MinHeight(20), GUILayout.MinWidth(100), GUILayout.ExpandHeight(true));
            }

            GuiUtils.ComboBox.DrawGUI();
        }

        void GetSliderInfo(string str, ref float target, float max, bool labelOnTop = false, bool maxSet = false)
        {
            GUILayout.BeginHorizontal();
            if (labelOnTop)
            {
                GUILayout.Label(" ", GUILayout.Width(85));
                GUILayout.FlexibleSpace();
                GUILayout.Label(str);
                GUILayout.FlexibleSpace();
                GUILayout.Label(" ", GUILayout.Width(75));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(" ", GUILayout.Width(60));
            }
            else
                GUILayout.Label(str, GUILayout.Width(60));
            float f = target;
            int m = max > 0 ? 1 : -1;

            if (GUILayout.Button("<<", GUILayout.Width(25)))
                target = f -= m * 0.5f;

            if (GUILayout.Button("◄", GUILayout.Width(25)))
                target = f -= m * 0.05f;

            if (maxSet)
                target = GUILayout.HorizontalSlider(f, 0, max, GUILayout.Width(200));
            else
                target = GUILayout.HorizontalSlider(f, -max, max, GUILayout.Width(200));

            if (GUILayout.Button("►", GUILayout.Width(25)))
                target = f += m * 0.05f;
            if (GUILayout.Button(">>", GUILayout.Width(25)))
                target = f += m * 0.5f;

            str = GUILayout.TextField(target.ToString("F3"), GUILayout.Width(50));
            if (float.TryParse(str, out f))
                target = f;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }


        // It will be useful to have a reference to the selected balancer
        ModulePWBFuelBalancer selBal = null;
        List<string> strings = new List<string>();
        List<string> selStrings;
        List<string> selStringsShort = new List<string> { "Compact", "Expanded" };
        List<string> selStringsLong = new List<string> { "Compact", "Select", "Expanded" };

        private void OnWindow(int windowId)
        {
            try
            {
                Rect rect = new Rect(_currentWindowPosition.width - 20, 4, 16, 16);
                if (GUI.Button(rect, "x", WhiteButton))
                {
                    toolbarControl.SetFalse(true);
                }
                GUILayout.BeginVertical();
                if (HighLogic.LoadedSceneIsFlight)
                {
                    
                    if (_listFuelBalancers.Count > 1) selStrings = selStringsLong;
                    else selStrings = selStringsShort;
                    int selGridInt = GUILayout.SelectionGrid((int)editMode, selStrings.ToArray(), 3); ;
                    editMode = (EditMode)selGridInt;
                    if (_listFuelBalancers.Count == 1 && editMode == EditMode.select)
                        editMode = EditMode.full;
                }
                else editMode = EditMode.full;

                strings.Clear();
                List<ModulePWBFuelBalancer>.Enumerator balancers = _listFuelBalancers.GetEnumerator();

                if (_listFuelBalancers.Count > 0)
                {
                    if (editMode >= EditMode.select)
                    {
                        while (balancers.MoveNext())
                        {
                            if (balancers.Current == null) continue;
                            if (balancers.Current.isPWBFuelBalancerPart)
                                strings.Add(balancers.Current.BalancerName);
                            else
                                strings.Add(balancers.Current.part.partInfo.title);
                        }
                        selectedBalancer = GuiUtils.ComboBox.Box(selectedBalancer, strings.ToArray(), this);
                        if (selectedBalancer != oldSelectedBalancer)
                        {
                            oldEditMode = EditMode.none;
                            if (oldSelectedBalancer != -1 && _listFuelBalancers[oldSelectedBalancer].SavedCoMMarkerVisible)
                                _listFuelBalancers[oldSelectedBalancer].ToggleSavedMarker();
                            oldSelectedBalancer = selectedBalancer;
                            activeSavedCoM = -1;
                        }
                    }

                    selBal = _listFuelBalancers[selectedBalancer];

                    // Provide a facility to change the name of the balancer
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Name: ");
                    string newName = GUILayout.TextField(selBal.BalancerName, GUILayout.Width(120));
                    if (HighLogic.LoadedSceneIsFlight && selBal.balanceStatus >= ModulePWBFuelBalancer.BalanceStatus.Maintaining)
                        GUILayout.Label("Active Balancer", YellowLabel);

                    if (HighLogic.LoadedSceneIsFlight && selBal.balanceStatus == ModulePWBFuelBalancer.BalanceStatus.Balance_not_possible)
                        GUILayout.Label("Balance Not Possible", YellowLabel);

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    if (selBal.BalancerName != newName && newName != "")
                        selBal.BalancerName = newName;
                    if (editMode == EditMode.full)
                    {
                        GetSliderInfo("▼ Max Slider Value ▼", ref selBal.maxVal, 20, true);
                        GUILayout.Space(10);
                        GetSliderInfo("Low/High:", ref selBal.VecFuelBalancerCoMTarget.y, selBal.maxVal);

                        if ((HighLogic.LoadedSceneIsEditor && EditorDriver.editorFacility == EditorFacility.SPH) ||
                                (HighLogic.LoadedSceneIsFlight && selBal.vessel.vesselType == VesselType.Plane))
                        {
                            GetSliderInfo("Fore/Aft:", ref selBal.VecFuelBalancerCoMTarget.z, -selBal.maxVal);
                            GetSliderInfo("Left/Right:", ref selBal.VecFuelBalancerCoMTarget.x, selBal.maxVal);
                            //
                        }
                        else
                        {
                            GetSliderInfo("Fore/Aft:", ref selBal.VecFuelBalancerCoMTarget.x, -selBal.maxVal);
                            GetSliderInfo("Left/Right:", ref selBal.VecFuelBalancerCoMTarget.z, -selBal.maxVal);
                        }
                    }
                    GUILayout.EndVertical();
                    //                   GUILayout.EndHorizontal();
                    string toggleText;
                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        GUILayout.Space(10);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Keep Balanced",
                            selBal.balanceStatus == ModulePWBFuelBalancer.BalanceStatus.Maintaining ? YellowButton : NormalButton,
                            GUILayout.Width(120)))
                        {
                            selBal.Maintain();
                        }
                        if (GUILayout.Button("Balance (one time)",
                            selBal.balanceStatus == ModulePWBFuelBalancer.BalanceStatus.Balancing ? YellowButton : NormalButton,
                            GUILayout.Width(120)))

                        {
                            selBal.BalanceFuel();
                        }
                        if (GUILayout.Button("Deactivate", GUILayout.Width(120)))
                        {
                            selBal.Disable();
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);
                    }
                    if (HighLogic.LoadedSceneIsFlight) // && selBal.isPWBFuelBalancerPart)
                    {
                        GUILayout.BeginHorizontal();

                        GUILayout.Label(" ", GUILayout.Width(120));

                        GUI.enabled = selBal.isPWBFuelBalancerPart || selBal.savedMarkers.Count > 0;
                        if (GUILayout.Button("Set CoM", GUILayout.Width(120)))
                        {
                            selBal.SetCoM();
                        }


                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();

                    GUI.enabled = selBal.VecFuelBalancerCoMTarget != ModulePWBFuelBalancer.NegVector;
                    toggleText = selBal.MarkerVisible ? "Hide Markers" : "Show Markers";

                    if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                    {
                        selBal.ToggleAllMarkers();
                    }

                    GUI.enabled = selBal.VecFuelBalancerCoMTarget != ModulePWBFuelBalancer.NegVector; // && selBal.SavedCoMMarker;
                    toggleText = selBal.SavedCoMMarkerVisible ? "Hide Saved CoM" : "Show Saved CoM";

                    if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                    {
                        selBal.ToggleSavedMarker();
                    }
                    GUI.enabled = true;

                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        toggleText = selBal.ActualCoMMarkerVisible ? "Hide Actual CoM" : "Show Actual CoM";

                        if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                        {
                            Log.Info("Balancer: " + selBal.BalancerName + " Toggle CoM Marker");
                            selBal.ToggleActualMarker();
                        }
                    }

                    GUILayout.EndHorizontal();

                    if (selBal.isPWBFuelBalancerPart)
                    {
                        // Save slot 1
                        GUILayout.BeginHorizontal();

                        selBal.Save1Name = GUILayout.TextField(selBal.Save1Name, activeSavedCoM == 0 ? YellowTextField : NormalTextField, GUILayout.Width(120));

                        GUI.enabled = selBal.VecSave1CoMTarget != ModulePWBFuelBalancer.NegVector;

                        if (GUILayout.Button("Load", GUILayout.Width(120)))
                        {
                            selBal.VecFuelBalancerCoMTarget = selBal.VecSave1CoMTarget;
                            activeSavedCoM = 0;
                        }

                        if (GUILayout.Button("Save", GUILayout.Width(120)))
                        {
                            selBal.VecSave1CoMTarget = selBal.VecFuelBalancerCoMTarget;
                            activeSavedCoM = 0;
                        }
                        GUILayout.EndHorizontal();
                        GUI.enabled = true;

                        // Save slot 2
                        GUILayout.BeginHorizontal();
                        selBal.Save2Name = GUILayout.TextField(selBal.Save2Name, activeSavedCoM == 1 ? YellowTextField : NormalTextField, GUILayout.Width(120));

                        GUI.enabled = selBal.VecSave2CoMTarget != ModulePWBFuelBalancer.NegVector;

                        if (GUILayout.Button("Load", GUILayout.Width(120)))
                        {
                            selBal.VecFuelBalancerCoMTarget = selBal.VecSave2CoMTarget;
                            activeSavedCoM = 1;
                        }

                        if (GUILayout.Button("Save", GUILayout.Width(120)))
                        {
                            selBal.VecSave2CoMTarget = selBal.VecFuelBalancerCoMTarget;
                            activeSavedCoM = 1;
                        }
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();

                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Add Marker", GUILayout.Width(120)))
                        {
                            selBal.savedMarkers.Add(new SavedMarker("", new Vector3(0, 0, 0)));
                            selBal.SetCoM();
                        }
                        GUI.enabled = selBal.VecFuelBalancerCoMTarget != ModulePWBFuelBalancer.NegVector; ;
                        toggleText = selBal.MarkerVisible ? "Hide Markers" : "Show Markers";

                        if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                        {
                            selBal.ToggleAllMarkers();
                        }

                        GUI.enabled = true;

                        toggleText = selBal.ActualCoLMarkerVisible ? "Hide CoL" : "Show CoL";
                        if (GUILayout.Button(toggleText, GUILayout.Width(120)))
                        {
                            Log.Info("Balancer: " + selBal.BalancerName + " Toggle CoL Marker");
                            selBal.ToggleCoLMarker();
                        }

                        GUILayout.EndHorizontal();

                        if (selBal.savedMarkers == null)
                            Log.Error("savedMarkers is null");
                        for (int i = 0; i < selBal.savedMarkers.Count; i++)
                        {
                            GUILayout.BeginHorizontal();
                            selBal.savedMarkers[i].name = GUILayout.TextField(selBal.savedMarkers[i].name, activeSavedCoM == i ? YellowTextField : NormalTextField, GUILayout.Width(120));

                            GUI.enabled = selBal.savedMarkers[i].marker != ModulePWBFuelBalancer.NegVector && selBal.savedMarkers[i].name != "";
                            if (GUILayout.Button("Load", GUILayout.Width(120)))
                            {
                                selBal.VecFuelBalancerCoMTarget = selBal.savedMarkers[i].marker;
                                activeSavedCoM = i;
                            }

                            if (GUILayout.Button("Save", GUILayout.Width(120)))
                            {
                                selBal.savedMarkers[i].marker = selBal.VecFuelBalancerCoMTarget;
                                activeSavedCoM = i;
                            }
                            GUI.enabled = true;
                            if (GUILayout.Button("X", GUILayout.Width(25)))
                            {
                                selBal.savedMarkers.Remove(selBal.savedMarkers[i]);
                            }

                            GUILayout.EndHorizontal();
                        }
                    }
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
                    GUILayout.EndVertical();
                }
                GUI.DragWindow();
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
            GameEvents.onPartExplode.Remove(OnPartExplode);
            GameEvents.onPartExplodeGroundCollision.Remove(OnPartExplodeGroundCollision);

            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.onHideUI.Remove(OnHideUI);
                GameEvents.onShowUI.Remove(OnShowUI);
            }

            if (toolbarControl != null)
            {
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
            }
            _listFuelBalancers.Clear();
            _listFuelBalancers = null;
        }

        private void BuildBalancerList()
        {
            if (_listFuelBalancers == null)
                return;
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
                    if (EditorLogic.RootPart != null && EditorLogic.RootPart.vessel != null && EditorLogic.RootPart.crossfeedPartSet != null)
                    {
                        BuildBalancerList(EditorLogic.RootPart.vessel.parts);
                    }
                }
            }
        }
        // Builds a list of off the ModulePWBFuelBalancers in the whole of the current vessel.
        private void BuildBalancerList(Vessel v)
        {
            if (v != null)
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
            for (int i = partList.Count - 1; i >= 0; i--)
            {
                var m = partList[i].Modules.GetModule<ModulePWBFuelBalancer>();
                if (m != null)
                    modList.Add(m);
            }
            List<ModulePWBFuelBalancer> sortedList = modList.OrderBy(o => o.BalancerName).ToList();
            return sortedList;
        }

#region EventHandlers
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
            BuildBalancerList(data);
        }
        private void OnEditorShipModified(ShipConstruct vesselConstruct)
        {
            if (vesselConstruct.Parts.Count == editorPartCount) return;
            BuildBalancerList(vesselConstruct.Parts);
            editorPartCount = vesselConstruct.parts.Count;
        }

        void OnEditorNewShipDialogDismiss()
        {
            if (_listFuelBalancers != null)
                _listFuelBalancers.Clear();
            else
                _listFuelBalancers = new List<ModulePWBFuelBalancer>();
            editorPartCount = 0;

            BuildBalancerList();
            if (EditorLogic.RootPart != null && EditorLogic.RootPart.vessel != null && EditorLogic.RootPart.vessel.parts != null)
                editorPartCount = EditorLogic.RootPart.vessel.parts.Count;
            else
                editorPartCount = 0;

        }
        private void OnEditorLoad(ShipConstruct vesselConstruct, CraftBrowserDialog.LoadType loadType)
        {
            BuildBalancerList(vesselConstruct.Parts);
            editorPartCount = vesselConstruct.parts.Count;
        }

        void OnPartExplode(GameEvents.ExplosionReaction er)
        {
            BuildBalancerList(FlightGlobals.ActiveVessel);
        }
        void OnPartExplodeGroundCollision(Part p)
        {
            BuildBalancerList(p.vessel.Parts);
        }

#endregion
    }
}