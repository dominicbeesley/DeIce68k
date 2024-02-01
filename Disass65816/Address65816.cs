using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816
{
    public class Address65816_far : DisassAddressBase
    {
        private UInt32 _address;

        public Address65816_far(UInt32 address)
        {
            _address = address & 0xFFFFFF;
        }

        public override long Canonical => (long)_address;

        public override UInt32 DeIceAddress => _address;

        public override object Clone()
        {
            return new Address65816_far((UInt32)Canonical);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            return other == null ? false : Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new Address65816_far((UInt32)(_address + b) & 0xFFFFFF);
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return (this.Canonical - b.Canonical) & 0xFFFFFF;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new Address65816_far((UInt32)(this._address - b) & 0xFFFFFF);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_address);
        }
        public override string ToString()
        {
            return $"${_address:X6}";
        }

    }

    public class Address65816_dp : DisassAddressBase, ILongFormAddress
    {

        private UInt32 _address;
        private UInt32 _dp;

        public Address65816_dp(UInt32 address, UInt32 dp = 0)
        {
            _address = address & 0xFF;
            _dp = dp & 0xFFFF;
        }

        public override long Canonical => (long)_address + (long)_dp;

        public override UInt32 DeIceAddress => (UInt32)(_address + (long)_dp);

        public override object Clone()
        {
            return new Address65816_dp(_address, _dp);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            return other == null ? false : Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new Address65816_far((UInt32)(_address + _dp + b) & 0xFFFFFF);
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return (this.Canonical - b.Canonical) & 0xFFFFFF;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new Address65816_far((UInt32)(this._address + this._dp - b) & 0xFFFFFF);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_address + _dp);
        }
        public override string ToString()
        {
            return $"${_address:X2}";
        }

        public string ToStringLong()
        {
            return $"${Canonical:X6}";
        }
    }

    public class Address65816_abs : DisassAddressBase
    {

        private UInt32 _address;
        private UInt32 _bank;

        public Address65816_abs(UInt32 address, UInt32 bank = 0)
        {
            _address = address & 0xFFFF;
            _bank = bank & 0xFF;
        }

        public override long Canonical => (long)_address + (long)(_bank << 16);

        public override UInt32 DeIceAddress => (UInt32)(_address + (long)(_bank << 16));

        public override object Clone()
        {
            return new Address65816_dp(_address, _bank);
        }

        public override int CompareTo(DisassAddressBase other)
        {
            return Canonical.CompareTo(other.Canonical);
        }

        public override bool Equals(DisassAddressBase other)
        {
            return other == null ? false : Canonical == other.Canonical;
        }

        protected override DisassAddressBase DoAddition(long b)
        {
            return new Address65816_far((UInt32)(_address + (_bank << 16) + b) & 0xFFFFFF);
        }

        protected override long DoSubtraction(DisassAddressBase b)
        {
            return (this.Canonical - b.Canonical) & 0xFFFFFF;
        }

        protected override DisassAddressBase DoSubtraction(long b)
        {
            return new Address65816_far((UInt32)(_address + (_bank << 16) - b) & 0xFFFFFF);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_address + (_bank << 16));
        }
        public override string ToString()
        {
            return $"${_address:X2}";
        }

        public string ToStringLong()
        {
            return $"${Canonical:X6}";
        }
    }


}
