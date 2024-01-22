using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface IDisassStateFactory
    {
        IDisassState Create();
    }
}
