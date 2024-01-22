using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816
{
    public class Address65816 : DisassAddressBase
    {
        private UInt32 _address;

        public Address65816(UInt32 address)
        {
            _address = address & 0xFFFFFF;
        }

        public override long Canonical => (long)_address;

        public override UInt32 DeIceAddress => _address;

        public override object Clone()
        {
            return new Address65816((UInt32)Canonical);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            return Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new Address65816((UInt32)(_address + b) & 0xFFFFFF);
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return (this.Canonical - b.Canonical) & 0xFFFFFF;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new Address65816((UInt32)(this._address - b) & 0xFFFFFF);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_address);
        }
        public override string ToString()
        {
            return _address.ToString("X6");
        }

    }
}
