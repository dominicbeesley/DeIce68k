using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReplySetWords : DeIceFnReplyBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_SET_WORDS;

        public ushort[] Data { get; }

        public DeIceFnReplySetWords(ushort[] data) => this.Data = data;

    }
}
