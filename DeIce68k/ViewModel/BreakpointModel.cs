using DeIce68k.Lib;
using DeIce68k.ViewModel.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class BreakpointModel : ObservableObject
    {
        private DeIceAppModel _app;

        public uint Address {
            get;
            init;
        }

        private bool _enabled;
        public bool Enabled {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        private ushort _oldOP;
        public ushort OldOP
        {
            get => _oldOP;
            set => Set(ref _oldOP, value);
        }

        public string SymbolStr
        {
            get
            {
                IEnumerable<string> syms;
                uint near_addr;
                if (_app.Symbols.FindNearest(Address, out syms, out near_addr))
                {
                    uint offset = Address - near_addr;
                    string o = (offset == 0) ? "" : $"+{offset:X2}";
                    return $"{syms.First()}{o}";
                }
                return "";
            }
        }


        private ScriptBase _conditionCode = null;
        public ScriptBase ConditionCode
        {
            get => _conditionCode;
            set => Set(ref _conditionCode, value);
        }


        public BreakpointModel(DeIceAppModel app)
        {
            _app = app;

            CmdEditCode = new RelayCommand(
                o =>
                {
                    EditCondition();
                },
                o =>
                {
                    return true;
                },
                "Edit Breakpoint Condition code",
                _app.Command_Exception
                );
        }

        public ICommand CmdEditCode { get; }

        public void EditCondition()
        {
            string code = (ConditionCode == null)?"return true;":ConditionCode.OrgCode;

            var dlg = new dlgEditCode(_app, code);
            if (dlg.ShowDialog() ?? false)
            {
                ConditionCode = dlg.CompiledCode;
            }
        }
    }
}
