using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public abstract class DeIceFnReplyRegsBase : DeIceFnReplyBase
    {
        public DeIceRegisters Registers { get; }

        internal DeIceFnReplyRegsBase(DeIceRegisters regs) => this.Registers = regs;
    }
}
