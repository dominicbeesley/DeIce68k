using DeIceProtocol;
using DisassShared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Transactions;

namespace DeIce68k.ViewModel
{
    public abstract class RegisterSetModelBase : ObservableObject
    {

        byte _targetStatus;
        public DeIceAppModel Parent { get; init; }

        public byte TargetStatus
        {
            get
            {
                return _targetStatus;
            }
            set
            {
                if (_targetStatus != value)
                {
                    _targetStatus = value;
                    RaisePropertyChangedEvent(nameof(TargetStatus));
                    RaisePropertyChangedEvent(nameof(IsStopped));
                    RaisePropertyChangedEvent(nameof(IsRunning));
                }
            }
        }

        public ReadOnlyObservableCollection<StatusRegisterBitsModel> StatusBits { get; init; }

        public bool IsStopped
        {
            get
            {
                return TargetStatus != DeIceProtoConstants.TS_RUNNING;
            }
        }

        public bool IsRunning
        {
            get
            {
                return !IsStopped;
            }
        }

        public abstract void FromDeIceProtocolRegData(byte[] deiceData);
        public abstract byte[] ToDeIceProtcolRegData();

        /// <summary>
        /// set in implementations where hardware trace is supported
        /// </summary>
        public abstract bool CanTrace { get; }

        /// <summary>
        /// Turn hardware trace on / off in registers
        /// </summary>
        /// <param name="trace"></param>
        public abstract bool SetTrace(bool trace);

        public abstract DisassAddressBase PCValue { get; set; }

        public RegisterModel RegByName(string name)
        {
            return
                this
                .GetType()
                .GetProperties()
                .Where(
                    r => r.Name == name && r.CanRead && r.PropertyType == typeof(RegisterModel)
                    ).FirstOrDefault()?.GetGetMethod().Invoke(this, new object[] { }) as RegisterModel;
        }

    }

    public static class RegisterSetModelExt 
    {
        public static T Clone<T>(this T regs) where T : RegisterSetModelBase, new()
        {
            T ret = new T();

            ret.FromDeIceProtocolRegData(regs.ToDeIceProtcolRegData());

            return ret;
        }
    }
}