using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqRun : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_RUN_TARG;
    }
}
