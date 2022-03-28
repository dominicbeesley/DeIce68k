using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyRun : DeIceFnReplyRegsBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_RUN_TARG;

        public DeIceFnReplyRun(byte[] regData) : base(regData) { }

    }
}
