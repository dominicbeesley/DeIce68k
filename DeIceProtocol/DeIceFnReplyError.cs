using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyError : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_ERROR;
        public byte ErrorCode { get; }

        internal DeIceFnReplyError(byte code)
        {
            ErrorCode = code;
        }
    }
}
