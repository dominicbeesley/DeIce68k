using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqSetWords : DeIceFnReqBase
    {
        public struct DeIceSetWordsWord
        {
            public uint Address { get; init; }
            public ushort Data { get; init; }
        }

        public override byte FunctionCode => DeIceProtoConstants.FN_SET_WORDS;

        public DeIceSetWordsWord[] Words {get; set; }

    }
}
