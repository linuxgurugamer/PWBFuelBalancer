using UnityEngine;

namespace PWBFuelBalancer
{
    public class SavedCoMMarker : MonoBehaviour
    {
        private ModulePWBFuelBalancer _linkedPart;

        internal ModulePWBFuelBalancer LinkPart {  set { _linkedPart = value; } }

        private void LateUpdate()
        {
            if (null == _linkedPart) return;
            Vector3 vecTargetComRotated = (_linkedPart.transform.rotation * Quaternion.Inverse(_linkedPart.RotationInEditor)) * _linkedPart.VecFuelBalancerCoMTarget;
            transform.position = _linkedPart.part.transform.position + vecTargetComRotated;
            if (HighLogic.LoadedSceneIsFlight)
            {
                transform.rotation = _linkedPart.vessel.transform.rotation;
            }
        }
    }
}
