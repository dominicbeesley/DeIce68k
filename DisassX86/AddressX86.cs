using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DisassX86
{
    public class AddressX86 : DisassAddressBase
    {
        public UInt16 Segment { get; init; }
        public UInt16 Offset { get; init; }

        public AddressX86(UInt16 segment, UInt16 offset)
        {
            Segment = segment;
            Offset = offset;
        }

        public AddressX86(UInt16 offset)
        {
            Segment = 0x0000;
            Offset = offset;
        }


        public override long Canonical => ((int)Segment << 4) + (int)Offset;

        public override UInt32 DeIceAddress => ((UInt32)Segment << 16) | ((UInt32)Offset);


        public override object Clone()
        {
            return new AddressX86(Segment, Offset);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            if (other == null) return false;
            return Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new AddressX86(Segment, (UInt16)(Offset + b));
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return this.Canonical - b.Canonical;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new AddressX86(Segment, (UInt16)(Offset - b));
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Canonical);
        }
        public override string ToString()
        {
            return $"{Segment:X4}:{Offset:X4}";
        }
    }
}
