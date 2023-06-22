using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DisassX86
{
    public class AddressX86Factory : IDisassAddressFactory
    {
        public DisassAddressBase FromCanonical(ulong canon)
        {
            return new AddressX86((UInt16)((canon & 0xF0000) >> 4), (UInt16)(canon & 0xFFFF));
        }

        public string AddressRegEx => @"[0-9a-f]{1,4}:[0-9a-f]{1,4}";

        public string AddressFormat => @"HHHHH:HHHH";



        Regex reSegOffs = new Regex(@"^\s*(([0-9a-f]{1,4}):)?([0-9a-f]{1,4})\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public DisassAddressBase Parse(string address)
        {
            var m = reSegOffs.Match(address);

            if (!m.Success)
                throw new FormatException("Badly formatted segment:offset address");

            if (!string.IsNullOrEmpty(m.Groups[2].Value))
            {
                return new AddressX86(Convert.ToUInt16(m.Groups[2].Value,16), Convert.ToUInt16(m.Groups[3].Value,16));
            } else
            {
                return new AddressX86(0, Convert.ToUInt16(m.Groups[3].Value));
            }
        
        }
    }
}
