using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816.Emulate
{
    public struct Tristate : IEquatable<Tristate>
    {
        private int Value { get; set; }

        public static readonly Tristate Unknown = new Tristate(-1);
        public static readonly Tristate False = new Tristate(0);
        public static readonly Tristate True = new Tristate(1);

        public bool IsUnknown => Value == Unknown.Value;

        public Tristate(bool v)
        {
            Value = v?True.Value:False.Value;
        }

        public Tristate()
        {
            Value = -1;
        }

        public Tristate(Tristate other)
        {
            Value = other.Value;
        }

        private Tristate(int val)
        {
            Value = val;
        }

        public static Tristate operator!(Tristate o)
        {
            return o == Unknown ? Tristate.Unknown : o == True ? False : True;
        }

        /// <summary>
        /// Compare two tristates for equality - if either is unknown returns false!
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static bool operator ==(Tristate a, Tristate b)
        {
            if (a.Value == Unknown.Value || b.Value == Unknown.Value)
                return false;
            else
                return a.Value == b.Value;
        }
        /// <summary>
        /// Compare two tristates for inequality - if either is unknown returns false!
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>

        public static bool operator !=(Tristate a, Tristate b)
        {
            if (a.Value == Unknown.Value || b.Value == Unknown.Value)
                return false;
            else
                return a.Value != b.Value;
        }

        public static implicit operator Tristate(bool b) { return new Tristate(b); }

        public bool Equals(Tristate other)
        {
            return other.Value == this.Value;
        }

        public bool ToBool(bool def) => this == Tristate.Unknown ? def : this == Tristate.True ? true : false;

        public override string ToString()
        {
            return Value == Unknown.Value ? "?" : Value == True.Value ? "T" : "F";
        }
    }

}
