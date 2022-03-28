using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyReadRegs : DeIceFnReplyRegsBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_READ_RG;

        public DeIceFnReplyReadRegs(byte[] regsdat) : base(regsdat) { }

    }
}
