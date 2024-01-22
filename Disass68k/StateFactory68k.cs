using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Disass68k
{
    internal class StateFactory68k : IDisassStateFactory
    {
        IDisassState IDisassStateFactory.Create() => new DisassState68k();
    }
}
