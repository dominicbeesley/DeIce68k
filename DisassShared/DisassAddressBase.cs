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
    public abstract class DisassAddressBase : ICloneable, IEquatable<DisassAddressBase>, IComparable<DisassAddressBase>
    {

        public abstract Int64 Canonical { get; }

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

    }
}
