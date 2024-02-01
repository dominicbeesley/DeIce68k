using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyWriteRegs : DeIceFnReplyStatusBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public DeIceFnReplyWriteRegs(bool success) : base(success) { }

    }
}
