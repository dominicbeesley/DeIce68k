using Disass65816;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassX86
{
    public class AddressFactory65816 : IDisassAddressFactory
    {
        internal AddressFactory65816() { }

        public string AddressRegEx => @"[0-9a-f]{1,6}";

        public string AddressFormat => @"HHHHHHH";

        public DisassAddressBase FromCanonical(ulong canonical)
        {
            return new Address65816_far((UInt32)canonical);
        }

        public DisassAddressBase Parse(string address)
        {
            return new Address65816_far(Convert.ToUInt32(address, 16));
        }
    }
}
