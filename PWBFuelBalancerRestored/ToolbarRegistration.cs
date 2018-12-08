using UnityEngine;
using ToolbarControl_NS;

namespace PWBFuelBalancer
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(PwbFuelBalancerAddon.MODID, PwbFuelBalancerAddon.MODNAME);
        }
    }
}