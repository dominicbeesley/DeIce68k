using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disass68k
{
    public interface IDisassSymbols
    {
        IEnumerable<string> AddressToSymbols(uint address);
        bool SymbolToAddress(string symbol, out uint address);
    }
}
