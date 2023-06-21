using Disass68k;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassX86
{
    public class AddressFactory68k : IDisassAddressFactory
    {
        internal AddressFactory68k() { }

        public string AddressRegEx => @"[0-9a-f]{1,8}";

        public string AddressFormat => @"HHHHHHHHH";

        public DisassAddressBase FromCanonical(ulong canonical)
        {
            return new Address68K((UInt32)canonical);
        }

        public DisassAddressBase Parse(string address)
        {
            return new Address68K(Convert.ToUInt32(address, 16));
        }
    }
}
