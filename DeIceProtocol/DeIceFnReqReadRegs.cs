using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqReadRegs : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_READ_RG;
    }
}
