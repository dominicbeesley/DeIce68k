using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassRiscV
{
    public class AddressFactoryRiscV : IDisassAddressFactory
    {
        internal AddressFactoryRiscV() { }

        public string AddressRegEx => @"[0-9a-f]{1,8}";

        public string AddressFormat => @"HHHHHHHHH";

        public DisassAddressBase FromCanonical(ulong canonical)
        {
            return new AddressRiscV((UInt32)canonical);
        }

        public DisassAddressBase Parse(string address)
        {
            return new AddressRiscV(Convert.ToUInt32(address, 16));
        }
    }
}
