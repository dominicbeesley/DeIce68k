using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public record DeIceFnReqWriteMem : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_WRITE_MEM;
          
        public uint Address { get; init; }

        public byte[] Data { get; init; }

        public DeIceFnReqWriteMem(uint address, byte [] data, int offset, int len)
        {
            if (data.Length > 255 - 5)
                throw new ArgumentException("Data too long");
            this.Data = new byte[len];
            Buffer.BlockCopy(data, offset, this.Data, 0, len);
            this.Address = address;
        }

    }
}
