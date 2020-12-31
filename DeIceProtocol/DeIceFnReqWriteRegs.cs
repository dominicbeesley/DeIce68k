using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReqWriteRegs : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public DeIceRegisters Regs { get; }

        public DeIceFnReqWriteRegs(DeIceRegisters regs) => this.Regs = regs;
    }
}
