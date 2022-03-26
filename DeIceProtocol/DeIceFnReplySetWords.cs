using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplySetBytes : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_SET_BYTES;

        public byte[] Data { get; }

        public DeIceFnReplySetBytes(byte[] data) => this.Data = data;

    }
}
