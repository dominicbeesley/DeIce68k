using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface IDisAss 
    {
        DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc);

        IDisassAddressFactory AddressFactory { get; }
    }
}
