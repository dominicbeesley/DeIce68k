using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDisassArm
{
    public class Symbols : IDisassSymbols
    {
        public IEnumerable<string> AddressToSymbols(uint address)
        {
            return Enumerable.Empty<string>();
        }

        public bool SymbolToAddress(string symbol, out uint address)
        {
            address = 0;
            return false;
        }
    }
}
