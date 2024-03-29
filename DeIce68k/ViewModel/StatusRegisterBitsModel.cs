﻿using DeIce68k.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class StatusRegisterBitsModel : ObservableObject
    {
        RegisterSetModelBase Parent { get; init; }

        bool _data = false;

        public string Label { get; init; }
        public string Name { get; init; }
        public int BitIndex { get; init; }
        public bool Data { 
            get
            {
                return _data;
            } 
            set
            {
                if (value != _data)
                {
                    _data = value;
                    RaisePropertyChangedEvent(nameof(Data));
                }
            }
        }

        public ICommand CmdToggle { get; init; }

        public StatusRegisterBitsModel(RegisterSetModelBase _parent)
        {
            this.Parent = _parent;
            CmdToggle = new RelayCommand(
                    o => { Data = !Data; },
                    o => { return Parent.IsStopped; },
                    "Status bit toggle",
                    _parent.Parent.Command_Exception
                );
        }
    }
}
