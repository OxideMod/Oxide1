using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide
{
    public struct DamageEvent
    {
        public string lol;
    }

    class MSILTest
    {
        protected virtual bool AdjustEvent(ref DamageEvent damage)
        {
            object result = Main.Call("ModifyDamage", new object[] { this, damage });
            if (result is DamageEvent)
            {
                damage = (DamageEvent)result;
                return true;
            }
            return false;
        }

    }
}
