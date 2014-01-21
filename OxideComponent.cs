using System;
using System.Collections.Generic;

using UnityEngine;

namespace Oxide
{
    public class OxideComponent : MonoBehaviour
    {
        public Main Oxide { get; set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);
        }
        public void Update()
        {
            if (Oxide != null) Oxide.Update();
        }
    }
}
