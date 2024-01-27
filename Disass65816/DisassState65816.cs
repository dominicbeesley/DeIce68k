using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816
{
    public class DisassState65816 : IDisassState
    {        
        
        /// <summary>
        /// Memory / Accumulator size true = 8bit
        /// </summary>
        public bool RegSizeM8 { get; set; }
        /// <summary>
        /// Index register size true = 8bit
        /// </summary>
        public bool RegSizeX8 { get; set; }

        public DisassState65816()
        {
            RegSizeM8 = true;
            RegSizeX8 = true;
        }

    }
}
