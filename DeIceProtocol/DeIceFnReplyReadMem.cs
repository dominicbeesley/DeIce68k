using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplyReadMem : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_READ_RG;

        public byte[] Data { get; }

        public DeIceFnReplyReadMem(byte[] data) => this.Data = data;

    }
}
