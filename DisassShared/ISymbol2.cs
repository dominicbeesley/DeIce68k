using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface ISymbol2<Taddr>
    {
        string Name { get; }
        Taddr Address { get; }
    }
}
