using UnityEngine;

namespace PWBFuelBalancer
{
    public class PwbCoMMarker : MonoBehaviour
    {
        private ModulePWBFuelBalancer _linkedPart;

        internal ModulePWBFuelBalancer LinkPart {  set { _linkedPart = value; } }

        private void LateUpdate()
        {
            if (null == _linkedPart) return;
            transform.position = _linkedPart.vessel.CoM;
            transform.rotation = _linkedPart.vessel.transform.rotation;
        }
    }

}
