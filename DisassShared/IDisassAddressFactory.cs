using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface IDisassAddressFactory
    {
        DisassAddressBase Parse(string address);

        DisassAddressBase FromCanonical(UInt64 other);

        string AddressRegEx { get; }
        string AddressFormat { get; }
    }
}
