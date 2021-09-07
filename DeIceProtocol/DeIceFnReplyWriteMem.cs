using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyWriteMem : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public bool Success { get; init; }

        public DeIceFnReplyWriteMem(bool success) { Success = success; }

    }
}
