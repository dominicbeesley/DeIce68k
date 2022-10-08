using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public record DisRec2<Taddr>
    {
        public bool Decoded { get; init; }
        public string Mnemonic { get; init; }
        public IEnumerable<DisRec2OperString_Base> Operands { get; init; }

        public string Hints { get; init; }

        public ushort Length { get; init; }

        public override string ToString() => $"{Mnemonic,-8} {Operands,-40}; {Hints}";

    }

    public record DisRec2OperString_Base
    {

    }

    public record DisRec2OperString_String : DisRec2OperString_Base
    { 
        public string Text {get; init;}

        public override string ToString()
        {
            return Text ?? "?";
        }
        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }
    }

    [Flags]
    public enum DisRec2_NumSize { 
        NONE = 0,
        U8 = 1,
        U16 = 2,
        U32 = 3,
        S8 = 5,
        S16 = 6,
        S32 = 7,
        SIGNED = 4,
        M_TYPE = 3
    }

    public record DisRec2OperString_Number : DisRec2OperString_Base
    {
        public SymbolType SymbolType { get; init; }
        public UInt32 Number { get; init; }
        public DisRec2_NumSize Size { get; init; }

        public override string ToString()
        {
            string m = "";
            if ((Size & DisRec2_NumSize.SIGNED) != 0)
            {
                int i = (int)Number;
                if (i < 0)
                {
                    m = "-";
                    i = -i;
                }

                if (i < 32)
                {
                    return $"{m}{i}";
                }
                else
                {
                    switch (Size) {

                        case DisRec2_NumSize.S8:
                            i = i & 0x7F;
                            return $"{m}0x{i:X}";
                        case DisRec2_NumSize.S16:
                            i = i & 0x7FFF;
                            return $"{m}0x{i:X}";
                        default:
                            return $"{m}0x{i:X}";
                    }
                }
            }
            else
            {
                UInt32 i = Number;

                if (i < 32)
                {
                    return $"{i}";
                }
                else
                {
                    switch (Size)
                    {
                        case DisRec2_NumSize.U8:
                            i = i & 0xFF;
                            return $"0x{i:X}";
                        case DisRec2_NumSize.U16:
                            i = i & 0xFFFF;
                            return $"0x{i:X}";
                        default:
                            return $"0x{i:X}";
                    }
                }
            }
        }   

        public override int GetHashCode()
        {
            return SymbolType.GetHashCode() + Number.GetHashCode();
        }

        public DisRec2OperString_Number()
        {
            Number = 0;
            Size = DisRec2_NumSize.U32;
        }
    }

    public record DisRec2OperString_Symbol : DisRec2OperString_Base
    {

        public ISymbol2<UInt32> Symbol { get; init; }

        public override string ToString()
        {
            return Symbol.Name;
        }

        public override int GetHashCode()
        {
            return Symbol.GetHashCode();
        }

    }

}
