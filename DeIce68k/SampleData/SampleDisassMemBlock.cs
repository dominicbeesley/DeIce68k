using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeIce68k.ViewModel;

namespace DeIce68k.SampleData
{
    static class SampleDisassMemBlock
    {
        public static DisassMemBlock Sample = new DisassMemBlock(
            null,
            0x8d080c,
            new byte[]
            {
                0x52, 0x01, 0x11, 0xc1, 0xfe, 0x00, 0x11, 0xC0, 0xFE, 0x01, 0x4e, 0x75, 0x99, 0x99, 0x99, 0x99
            },
            new Dictionary<uint, string>
            {
                [0x8d080c] = "bob",
                [0xFFFFFE00] = "sheila_crtc_reg",
                [0xFFFFFE01] = "sheila_crtc_rw",
            }
            );
    }
}
