using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    [Flags]
    public enum SymbolType
    {
        NONE = 0,
        Pointer = 1,
        ServiceCall = 2,
        Immediate = 4,
        //TODO:if offset is combined with pointer it is processor dependent for meaning i.e. for 65816 it means direct page
        Offset = 8,
        Port = 16,
        ANY = 63
    }
}
