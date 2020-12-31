using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{

    [Flags]
    public enum DeIceTargetOptionFlags
    {
        HasFNStep = 0x10,
        HasFNResetTarget = 0x20,
        HasFNStopTarget = 0x40,
        HasFNCall = 0x80
    }
}
