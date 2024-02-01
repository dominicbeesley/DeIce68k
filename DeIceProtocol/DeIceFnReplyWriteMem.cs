using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyWriteMem : DeIceFnReplyStatusBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_RG;

        public DeIceFnReplyWriteMem(bool success) : base(success) { }

    }
}
