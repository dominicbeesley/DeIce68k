using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface IDisassSymbols
    {
        IEnumerable<string> AddressToSymbols(uint address);
        bool SymbolToAddress(string symbol, out uint address);
    }
}
