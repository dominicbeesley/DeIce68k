using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816
{
    internal class StateFactory65816 : IDisassStateFactory
    {
        IDisassState IDisassStateFactory.Create() => new DisassState65816();
    }
}
