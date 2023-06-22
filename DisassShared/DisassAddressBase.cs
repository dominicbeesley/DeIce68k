using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    /// <summary>
    /// Represents a single address in a CPU's address space
    /// </summary>
    public abstract class DisassAddressBase : ICloneable, IEquatable<DisassAddressBase>, IComparable<DisassAddressBase>, IComparable
    {

        public abstract Int64 Canonical { get; }

        /// <summary>
        /// An address suitable for passing to DeIce protocol functions
        /// </summary>
        public abstract UInt32 DeIceAddress { get; }

        /// <summary>
        /// Returns the signed distance in bytes between the two addresses
        /// </summary>
        public static Int64 operator -(DisassAddressBase a, DisassAddressBase b)
        {
            return a.DoSubtraction(b);
        }

        public static DisassAddressBase operator -(DisassAddressBase a, Int64 b)
        {
            return a.DoSubtraction(b);
        }


        public static DisassAddressBase operator +(DisassAddressBase a, Int64 b)
        {
            return a.DoAddition(b);
        }


        public static bool operator <(DisassAddressBase a,  DisassAddressBase b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(DisassAddressBase a, DisassAddressBase b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(DisassAddressBase a, DisassAddressBase b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(DisassAddressBase a, DisassAddressBase b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static DisassAddressBase Empty { get; }

        protected abstract Int64 DoSubtraction(DisassAddressBase b);
        protected abstract DisassAddressBase DoSubtraction(Int64 b);
        protected abstract DisassAddressBase DoAddition(Int64 b);

        public abstract object Clone();

        public abstract bool Equals(DisassAddressBase other);

        public abstract int CompareTo(DisassAddressBase other);

        public override abstract string ToString();
        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            } else
            {
                return Equals((DisassAddressBase)obj);
            }
        }

        public int CompareTo(object obj)
        {
            throw new NotImplementedException();
        }

        public override abstract int GetHashCode();
    }
}
