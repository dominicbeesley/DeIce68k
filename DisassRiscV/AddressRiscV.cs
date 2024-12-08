using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassRiscV
{
    public class AddressRiscV : DisassAddressBase
    {
        private UInt32 _address;

        public AddressRiscV(UInt32 address)
        {
            _address = address;
        }

        public override long Canonical => (long)_address;

        public override UInt32 DeIceAddress => _address;


        public override object Clone()
        {
            return new AddressRiscV((UInt32)Canonical);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            if (other == null)
                return false;
            return Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new AddressRiscV((UInt32)(_address + b));
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return this.Canonical - b.Canonical;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new AddressRiscV((UInt32)(this._address + b));
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_address);
        }
        public override string ToString()
        {
            return _address.ToString("X8");
        }

    }
}
