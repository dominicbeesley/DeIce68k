using System;
using System.Collections.Generic;
using System.Text;

namespace DeIceProtocol
{
    public record DeIceRegisters
    {
        public byte TargetStatus { get; init; }
        public UInt32 A7u { get; init; }
        public UInt32 A7s { get; init; }
        public UInt32 A6 { get; init; }
        public UInt32 A5 { get; init; }
        public UInt32 A4 { get; init; }
        public UInt32 A3 { get; init; }
        public UInt32 A2 { get; init; }
        public UInt32 A1 { get; init; }
        public UInt32 A0 { get; init; }

        public UInt32 D7 { get; init; }
        public UInt32 D6 { get; init; }
        public UInt32 D5 { get; init; }
        public UInt32 D4 { get; init; }
        public UInt32 D3 { get; init; }
        public UInt32 D2 { get; init; }
        public UInt32 D1 { get; init; }
        public UInt32 D0 { get; init; }

        public UInt32 PC { get; init; }

        public UInt16 SR { get; init; }

        public string PrettyString()
        {
            return
@$"D0 : {D0:X8}   A0 : {A0:X8}
D1 : {D1:X8}   A1 : {A1:X8}
D2 : {D2:X8}   A2 : {A2:X8}
D3 : {D3:X8}   A3 : {A3:X8}
D4 : {D4:X8}   A4 : {A4:X8}
D5 : {D5:X8}   A5 : {A5:X8}
D6 : {D6:X8}   A6 : {A6:X8}
D7 : {D7:X8}   A7u: {A7u:X8}
                A7s: {A7s:X8}
PC : {PC:X8}   SR :     {SR:X4}
";
        }
    }
}
