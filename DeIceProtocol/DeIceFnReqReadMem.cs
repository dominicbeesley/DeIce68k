using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public class DeIceFnReqReadMem : DeIceFnReqBase
    {
        public override byte FunctionCode => DeIceProtoConstants.FN_READ_MEM;
          
        public uint Address { get; set; }

        public byte Len { get; set; }

        public DeIceFnReqReadMem()
        {

        }
    }
}
