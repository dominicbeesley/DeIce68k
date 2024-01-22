using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassX86
{
    internal class DisassStateX86 : IDisassState
    {        
        
        /// <summary>
        /// Memory / Accumulator size true = 8bit
        /// </summary>
        public bool RegSizeM8 { get; set; }
        /// <summary>
        /// Index register size true = 8bit
        /// </summary>
        public bool RegSizeX8 { get; set; }

        public DisassStateX86()
        {
            RegSizeM8 = true;
            RegSizeX8 = true;
        }

    }
}
