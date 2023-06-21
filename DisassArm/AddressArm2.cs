using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassArm
{
    public class AddressArm2 : DisassAddressBase
    {
        private UInt32 _address;

        public AddressArm2(UInt32 address)
        {
            _address = address & 0x03FFFFFF;
        }

        public override long Canonical => (long)_address;

        public override object Clone()
        {
            return new AddressArm2((UInt32)Canonical);
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
            return new AddressArm2((UInt32)(_address + b) & 0x03FFFFFF);
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return this.Canonical - b.Canonical;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new AddressArm2((UInt32)(this._address + b) & 0x03FFFFFF);
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
