using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqReadMem : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_READ_MEM;
          
        public uint Address { get; init; }

        public byte Len { get; init; }

    }
}
