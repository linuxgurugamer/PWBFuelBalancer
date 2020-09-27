
using UnityEngine;
using KSP_Log;

namespace PWBFuelBalancer
{
    public class WeightedVectorAverager
    {
        private Vector3 _sum = Vector3.zero;
        private float _totalWeight;


        public void Add(Vector3 v, float weight)
        {
            _sum += v * weight;
            _totalWeight += weight;
        }


        public Vector3 Get()
        {
            if (_totalWeight > 0f)
                return _sum / _totalWeight;

            return Vector3.zero;
        }


        public float GetTotalWeight()
        {
            return _totalWeight;
        }


        public void Reset()
        {
            _sum = Vector3.zero;
            _totalWeight = 0f;
        }
    }
    public class PwbCoLMarker : MonoBehaviour
    {
        internal static Log Log = new Log("PWBFuelBalance.PwbCoLMarker");

        private ModulePWBFuelBalancer _linkedPart;
        ArrowData _centerOfLift;
        private readonly CenterOfLiftQuery _centerOfLiftQuery = new CenterOfLiftQuery();
        private readonly WeightedVectorAverager _weightedPositionAvg = new WeightedVectorAverager();
        private readonly WeightedVectorAverager _weightedDirectionAvg = new WeightedVectorAverager();
        private static readonly ArrowData _zeroArrowData = new ArrowData(Vector3.zero, Vector3.zero, 0f);
        internal ModulePWBFuelBalancer LinkPart { set { _linkedPart = value; } }


        private struct ArrowData
        {
            public Vector3 Position { get; }
            public Vector3 Direction { get; }
            public float Total { get; }


            public ArrowData(Vector3 position, Vector3 direction, float total)
            {
                Position = position;
                Direction = direction;
                Total = total;
            }
        }

        void Start()
        {
#if DEBUG
            Log.SetLevel(Log.LEVEL.INFO);
#else
                Log.SetLevel(Log.LEVEL.ERROR);
#endif

            UpdateSettings();
            GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
        }
        private void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
        }

        private void LateUpdate()
        {
            if (_linkedPart != null &&_linkedPart.vessel.staticPressurekPa > 0f)
            {
                _centerOfLift = FindCenterOfLift(_linkedPart.vessel.rootPart, _linkedPart.vessel.srf_velocity, _linkedPart.vessel.altitude,
                    _linkedPart.vessel.staticPressurekPa, _linkedPart.vessel.atmDensity);

                transform.position = _centerOfLift.Position;
                transform.rotation = _linkedPart.vessel.transform.rotation;
            }
        }

        private ArrowData FindCenterOfLift(Part rootPart, Vector3 refVel, double refAlt, double refStp, double refDens)
        {
            _weightedPositionAvg.Reset();
            _weightedDirectionAvg.Reset();

            RecurseCenterOfLift(rootPart, refVel, refAlt, refStp, refDens);

            return Mathf.Approximately(_weightedPositionAvg.GetTotalWeight(), 0f) ? _zeroArrowData :
                new ArrowData(_weightedPositionAvg.Get(), _weightedDirectionAvg.Get(), _weightedPositionAvg.GetTotalWeight());
        }


        private void RecurseCenterOfLift(Part part, Vector3 refVel, double refAlt, double refStp, double refDens)
        {
            var count = part.Modules.Count;
            while (count-- > 0)
            {
                var module = part.Modules[count] as ILiftProvider;
                if (module == null)
                    continue;

                _centerOfLiftQuery.Reset();
                _centerOfLiftQuery.refVector = refVel;
                _centerOfLiftQuery.refAltitude = refAlt;
                _centerOfLiftQuery.refStaticPressure = refStp;
                _centerOfLiftQuery.refAirDensity = refDens;

                module.OnCenterOfLiftQuery(_centerOfLiftQuery);

                _weightedPositionAvg.Add(_centerOfLiftQuery.pos, _centerOfLiftQuery.lift);
                _weightedDirectionAvg.Add(_centerOfLiftQuery.dir, _centerOfLiftQuery.lift);
            }

            count = part.children.Count;
            for (var i = 0; i < count; i++)
            {
                RecurseCenterOfLift(part.children[i], refVel, refAlt, refStp, refDens);
            }
        }
        private void OnGameSettingsApplied()
        {
            UpdateSettings();
        }

        // PWBSettings
        private void UpdateSettings()
        {
            CenterOfLiftCutoff = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().LiftCutoff;
            ArrowLength = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().ArrowLength;
            MaxArrowTopSpeed = HighLogic.CurrentGame.Parameters.CustomParams<PWBSettings>().MaxArrowTopSpeed;
        }


        private void OnGUI()
        {
            //if (_linkedPart.vessel.srf_velocity.magnitude > 0.1f)
            {
                DrawTools.NewFrame();
            }
        }
        private void OnRenderObject()
        {
            //if (_linkedPart.vessel.srf_velocity.magnitude > 0.1f)
            {
                //Log.Info("OnRenderObject: _linkedPart.vessel.srf_velocity.magnitude: " + _linkedPart.vessel.srf_velocity.magnitude);
                OnRenderObjectEvent();
            }
        }

        public static int CenterOfLiftCutoff = 10;
        private static float ArrowLength = 4.0f;
        private static float MaxArrowTopSpeed = 300;
        private float calcArrowLength
        {
            get
            {
                if (_linkedPart.vessel.srf_velocity.magnitude >= MaxArrowTopSpeed)
                    return ArrowLength;
                return (float)_linkedPart.vessel.srf_velocity.magnitude / MaxArrowTopSpeed * ArrowLength;
            }
        }
        private void OnRenderObjectEvent()
        {
            if ( /* Camera.current != Camera.main || */ MapView.MapIsEnabled) return;

            if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA &&
                _linkedPart.vessel == FlightGlobals.ActiveVessel)
                return;

            if (_linkedPart.vessel != FlightGlobals.ActiveVessel)
            {
                if (Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, _linkedPart.vessel.transform.position) >
                    PhysicsGlobals.Instance.VesselRangesDefault.subOrbital.unload)
                {
                    return;
                }
            }

            if (_centerOfLift.Total > CenterOfLiftCutoff)
            {
                DrawTools.DrawArrow(_centerOfLift.Position, _centerOfLift.Direction.normalized * calcArrowLength, XKCDColors.Blue);
            }
        }
    }
}
