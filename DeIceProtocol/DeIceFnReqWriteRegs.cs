using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqWriteRegs : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public byte[] RegData { get; init; }
    }
}
