using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassArm
{
    public class AddressFactoryArm2 : IDisassAddressFactory
    {
        internal AddressFactoryArm2() { }

        public string AddressRegEx => @"[0-9a-f]{1,8}";

        public string AddressFormat => @"HHHHHHHHH";

        public DisassAddressBase FromCanonical(ulong canonical)
        {
            return new AddressArm2((UInt32)canonical);
        }

        public DisassAddressBase Parse(string address)
        {
            return new AddressArm2(Convert.ToUInt32(address, 16));
        }
    }
}
