using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyWriteRegs : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public bool Success { get; }

        public DeIceFnReplyWriteRegs(bool success) => this.Success = success;

    }
}
