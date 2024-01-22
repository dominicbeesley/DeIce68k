using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DisassX86
{
    internal class StateFactoryX86 : IDisassStateFactory
    {
        IDisassState IDisassStateFactory.Create() => new DisassStateX86();
    }
}
