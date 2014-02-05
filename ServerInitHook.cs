using System;

using UnityEngine;

namespace Oxide
{
    public class ServerInitHook : MonoBehaviour
    {
        public void OnDestroy()
        {
            Main.Call("OnServerInitialized", null);
        }

    }
}
