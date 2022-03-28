using DeIceProtocol;
using System.Collections.ObjectModel;
using System.ComponentModel;

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

        public abstract uint PCValue { get; }

    }
}