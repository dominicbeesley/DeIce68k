using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqSetBytes : DeIceFnReqBase
    {
        public struct DeIceSetBytesItem
        {
            public uint Address { get; init; }
            public byte Data { get; init; }
        }

        public override byte FunctionCode => DeIceProtoConstants.FN_SET_BYTES;

        public DeIceSetBytesItem[] Items {get; set; }

    }
}
