﻿using DeIce68k.Lib;
using DeIceProtocol;
using Disass65816;
using DisassArm;
using DisassShared;
using DossySerialPort;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// main application model
    /// </summary>
    public class DeIceAppModel : ObservableObject
    {
        private DeIceFnReplyGetStatus _debugHostStatus;
        public DeIceFnReplyGetStatus DebugHostStatus
        {
            get
            {
                if (_debugHostStatus == null)
                {
                    //return a default...
                    return DeIceFnReplyGetStatus.Default;
                }
                else
                {
                    return _debugHostStatus;
                }
            }
            set
            {
                UpdateHostStatus(value);
            }
        }

        bool _sampleData = false; // set in constructor if this is a sample data instance i.e. don't actually do any protocol stuff!

        public IDossySerial Serial { get; init; }

        DeIceProtocolMain _deIceProto;
        public DeIceProtocolMain DeIceProto { get { return _deIceProto; } }

        ObservableCollection<WatchModel> _watches = new ObservableCollection<WatchModel>();
        public ObservableCollection<WatchModel> Watches { get { return _watches; } }

        ObservableCollection<BreakpointModel> _breakpointsint = new ObservableCollection<BreakpointModel>();
        ReadOnlyObservableCollection<BreakpointModel> _breakpoints;
        public ReadOnlyObservableCollection<BreakpointModel> Breakpoints { get { return _breakpoints; } }


        RegisterSetModelBase _regs;

        public RegisterSetModelBase Regs { get { return _regs; } }

        ObservableCollection<string> _messages = new ObservableCollection<string>();
        public ObservableCollection<string> Messages { get { return _messages; } }

        int mn = 0;
        object mnLock = new object();

        public DeIceSymbols Symbols { get; }


        //TODO: Dispose
        CancellationTokenSource traceCancelSource = null;


        private DisassMemBlock _disassMemBlock;
        public DisassMemBlock DisassMemBlock
        {
            get
            {
                return _disassMemBlock;
            }
            set
            {
                _disassMemBlock = value;
                this.RaisePropertyChangedEvent(nameof(DisassMemBlock));
            }
        }

        private int _MessageNo()
        {
            lock (mnLock)
                return mn++;
        }

        public void AppendMessage(string s)
        {
            Messages.Add($"{_MessageNo():X4} {s}");
        }


        public ICommand CmdNext { get; }
        public ICommand CmdCont { get; }
        public ICommand CmdTraceTo { get; }
        public ICommand CmdDumpMem { get; }
        public ICommand CmdStop { get; }
        public ICommand CmdRefresh { get; }
        public ICommand CmdDisassembleAt { get; }

        public ICommand CmdBreakpoints_Add { get; }
        public ICommand CmdBreakpoints_Delete { get; }
        public ICommand CmdWatches_Add { get; }
        public ICommand CmdWatches_Delete { get; }


        public ICommand CmdLoadBinary { get; }
        public ICommand CmdSaveBinary { get; }
        public ICommand CmdRunScript { get; }
        public ICommand CmdOpenAScript { get; }
        public ICommand CmdClearLog { get; }

        public MainWindow MainWindow { get; init; }



        private bool _busyInt;
        protected bool BusyInt
        {
            get => _busyInt;
            set
            {
                Set(ref _busyInt, value);
                RaisePropertyChangedEvent(nameof(HostBusy));
            }
        }

        /// <summary>
        /// This is true if the host is running or there is a long load/read running
        /// </summary>
        public bool HostBusy
        {
            get => _busyInt || !(Regs?.IsStopped ?? false);
        }


        public void ReadCommandFile(string pathname)
        {
            new DeIceScript(this).ExecuteScript(pathname);
            AddRecentCommandFile(pathname);
        }

        private ObservableCollection<string> _recentCommandFiles;
        public ReadOnlyObservableCollection<string> RecentCommandFiles { get; }

        private void AddRecentCommandFile(string pathname)
        {
            int ix = _recentCommandFiles.IndexOf(pathname);
            if (ix > 0)
            {
                _recentCommandFiles.RemoveAt(ix);
                _recentCommandFiles.Insert(0, pathname);
            }
            else if (ix < 0)
            {
                _recentCommandFiles.Insert(0, pathname);
            }

            while (_recentCommandFiles.Count > 10)
            {
                _recentCommandFiles.RemoveAt(_recentCommandFiles.Count - 1);
            }
        }


        private DisassAddressBase _loadBinaryAddr_last = null;
        public DisassAddressBase LoadBinaryAddr_last
        {
            get => _loadBinaryAddr_last;
            set => Set(ref _loadBinaryAddr_last, value);
        }

        private string _loadBinaryFilename_last = "";
        public string LoadBinaryFilename_last
        {
            get => _loadBinaryFilename_last;
            set => Set(ref _loadBinaryFilename_last, value);
        }

        private DisassAddressBase _saveBinaryAddr_last = null;
        public DisassAddressBase SaveBinaryAddr_last
        {
            get => _saveBinaryAddr_last;
            set => Set(ref _saveBinaryAddr_last, value);
        }

        private string _saveBinaryFilename_last = "";
        public string SaveBinaryFilename_last
        {
            get => _saveBinaryFilename_last;
            set => Set(ref _saveBinaryFilename_last, value);
        }

        private long _saveBinaryLength_last = 0;
        public long SaveBinaryLength_last
        {
            get => _saveBinaryLength_last;
            set => Set(ref _saveBinaryLength_last, value);
        }


        public BackgroundWorker LoadBinaryFile(DisassAddressBase addr, string filename)
        {
            BackgroundWorker b = new BackgroundWorker();
            b.WorkerSupportsCancellation = true;
            var prg = new DlgProgress();
            prg.Owner = MainWindow;
            prg.Title = "Loading Binary...";
            prg.Message = $"Loading file {filename}";
            prg.Progress = 0;
            prg.Show();
            prg.Cancel += (o, e) => { b.CancelAsync(); };
            BusyInt = true;
            b.DoWork += (o, e) =>
            {
                try
                {
                    try
                    {
                        using (var rd = new FileStream(filename, FileMode.Open, FileAccess.Read))
                        {


                            int maxlen = DebugHostStatus.ComBufSize - 6;
                            byte[] buf = new byte[maxlen];
                            long len = rd.Length;
                            long done = 0;
                            int cur = 0;
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            long last = sw.ElapsedMilliseconds - 200;
                            do
                            {
                                cur = rd.Read(buf, 0, maxlen);
                                try
                                {
                                    DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteMem>(new DeIceFnReqWriteMem(addr.DeIceAddress, buf, 0, cur));
                                }
                                catch (DeIceProtocolException ex)
                                {
                                    throw new Exception($"Error copying data at {addr:X08}", ex);
                                }
                                addr += (uint)cur;
                                done += cur;

                                if (cur == 0 || sw.ElapsedMilliseconds - last >= 200)
                                {
                                    MainWindow.Dispatcher.BeginInvoke(() =>
                                    {
                                        prg.Progress = (double)(100 * done) / (double)len;
                                    });
                                    last = sw.ElapsedMilliseconds;
                                }


                            } while (cur > 0 && !b.CancellationPending);

                        }
                    }
                    catch (Exception ex)
                    {
                        DoInvoke(() =>
                        {
                            Messages.Add($"Error loading binary file {ex.Message}");
                        });
                    }
                }
                finally
                {
                    DoInvoke(() =>
                    {
                        prg.Close();
                        BusyInt = false;
                    });
                }
            };

            LoadBinaryAddr_last = addr;
            LoadBinaryFilename_last = filename;

            b.RunWorkerAsync();
            return b;

        }
        public BackgroundWorker SaveBinaryFile(DisassAddressBase addr, long length, string filename)
        {
            BackgroundWorker b = new BackgroundWorker();
            b.WorkerSupportsCancellation = true;
            var prg = new DlgProgress();
            prg.Owner = MainWindow;
            prg.Title = "Saving Binary...";
            prg.Message = $"Saving file {filename}";
            prg.Progress = 0;
            prg.Show();
            prg.Cancel += (o, e) => { b.CancelAsync(); };
            BusyInt = true;
            b.DoWork += (o, e) =>
            {
                try
                {
                    try
                    {
                        using (var f_wr = new FileStream(filename, FileMode.Create, FileAccess.Write))
                        {

                            byte maxlen = (byte)(DebugHostStatus.ComBufSize - 6);
                            byte[] buf = new byte[maxlen];
                            long len = length;
                            long done = 0;
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            long last = sw.ElapsedMilliseconds - 200;
                            while (len > 0 && !b.CancellationPending)
                            {
                                int cur = (len > maxlen)? maxlen : (int)len;

                                var reply = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr.DeIceAddress, Len = (byte)cur });
                                f_wr.Write(reply.Data, 0, reply.Data.Length);
                                addr += cur;
                                done += cur;
                                len -= cur;

                                if (cur == 0 || sw.ElapsedMilliseconds - last >= 200)
                                {
                                    MainWindow.Dispatcher.BeginInvoke(() =>
                                    {
                                        prg.Progress = (double)(100 * done) / (double)length;
                                    });
                                    last = sw.ElapsedMilliseconds;
                                }


                            } 

                        }
                    }
                    catch (Exception ex)
                    {
                        DoInvoke(() =>
                        {
                            Messages.Add($"Error loading binary file {ex.Message}");
                        });
                    }
                }
                finally
                {
                    DoInvoke(() =>
                    {
                        prg.Close();
                        BusyInt = false;
                    });
                }
            };

            SaveBinaryAddr_last = addr;
            SaveBinaryLength_last = length;
            SaveBinaryFilename_last = filename;

            b.RunWorkerAsync();
            return b;

        }

        public void ParseSymbolOrAddress(string s, out string name, out DisassAddressBase addr)
        {
            name = null;
            ISymbol2 sym;
            if (Symbols.FindByName(s, out sym))
            {
                name = sym.Name;
                addr = sym.Address;
            }
            else
            {
                //couldn't find symbol try as address
                try
                {
                    addr = GetDisass().AddressFactory.Parse(s);
                }
                catch (Exception)
                {
                    throw new ArgumentException($"\"{s}\" is not a known symbol or address number");
                }
                name = Symbols.FindNearest(addr, SymbolType.ANY);
            }

        }

        protected void Breakpoint_Changed(object sender, PropertyChangedEventArgs e)
        {
            DisassMemBlock?.BreakpointsUpdated();
        }

        public DeIceAppModel(IDossySerial serial, MainWindow mainWindow, bool sampleData = false, DeIceFnReplyGetStatus hostStatus = null)
        {
            this._sampleData = sampleData;
            this.MainWindow = mainWindow;
            this.Serial = serial;

            _recentCommandFiles = new ObservableCollection<string>();
            RecentCommandFiles = new ReadOnlyObservableCollection<string>(_recentCommandFiles);
            _breakpoints = new ReadOnlyObservableCollection<BreakpointModel>(_breakpointsint);

            _regs = null;

            Symbols = new DeIceSymbols(this);

            _breakpointsint.CollectionChanged += (o, e) =>
            {
                DisassMemBlock?.BreakpointsUpdated();
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (BreakpointModel item in e.OldItems)
                    {
                        //Removed items
                        item.PropertyChanged -= Breakpoint_Changed;
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (BreakpointModel item in e.NewItems)
                    {
                        //Added items
                        item.PropertyChanged += Breakpoint_Changed;
                    }
                }
            };

            CmdNext = new RelayCommand(
            o =>
            {
                try
                {
                    if (Regs != null)
                    {
                        if (Regs.TargetStatus == DeIceProtoConstants.TS_BP)
                            if (ReExecCurBreakpoint())
                            {
                                RunFinish(true);
                                return;
                            }

                        var rr = Exec_SingleStep();
                        if (rr != null)
                        {
                            Regs.FromDeIceProtocolRegData(rr.RegisterData);
                            //TODO: Special status instead of BP/TACE here ?
                            RunFinish(true);
                            return;
                        }
                    }
                    AppendMessage($"ERROR: Unable to Execute Next");
                }
                catch (Exception ex)
                {
                    AppendMessage($"ERROR:Executing Next\n{ex.ToString()} ");
                    Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                }
            },
            o =>
            {
                return (Regs?.IsStopped ?? false) && ((Regs?.CanTrace ?? false) || Regs is IRegisterSetPredictNext);
            },
            "Step Next",
            Command_Exception
            )
            {
            };
            CmdCont = new RelayCommand(
                o =>
                {
                    DoContinue();
                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Continue",
                Command_Exception

            );

            CmdTraceTo = new RelayCommand(
                o =>
                {
                    var dlg = new DlgTraceTo(this);
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;
                    //TODO: use binding and a viewmodel?

                    if (dlg.ShowDialog() == true)
                    {
                        TraceTo(dlg.Address);
                    }

                },
                o => { return (Regs?.IsStopped ?? false) && ((Regs?.CanTrace ?? false) || Regs is IRegisterSetPredictNext); },
                "Trace To...",
                Command_Exception
                );

            CmdDumpMem = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Dump Memory";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;
                    //TODO: use binding and a viewmodel?

                    if (dlg.ShowDialog() == true)
                    {
                        byte[] buf = new byte[256];
                        DeIceProto.ReadMemBlock(dlg.Address.DeIceAddress, buf, 0, 256);

                        bool first = true;
                        StringBuilder l = new StringBuilder();
                        StringBuilder l2 = new StringBuilder();
                        DisassAddressBase a = dlg.Address;
                        for (int i = 0; i < 256; i++)
                        {
                            var rowoffs = (int)(a.Canonical % 16);
                            if (rowoffs == 0 || first)
                            {
                                first = false;
                                if (l.Length > 0)
                                    Messages.Add($"{l} | {l2}");
                                l.Clear();
                                l2.Clear();
                                l.Append($"{a - rowoffs} : ");
                                for (int j = 0; j < rowoffs; j++)
                                {
                                    l.Append("   ");
                                    l2.Append(" ");
                                }
                            }
                            l.Append($" {buf[i]:X2}");
                            if (buf[i] > 32 && buf[i] < 128)
                                l2.Append((char)buf[i]);
                            else
                                l2.Append(".");
                            a = a + 1;
                        }
                        if (l.Length > 0)
                            Messages.Add($"{l} | {l2}");
                    }

                },
                o => { return Regs?.IsStopped ?? false; },
                "Dump Memory",
                Command_Exception
            );


            CmdStop = new RelayCommand(
                o =>
                {
                    if (traceCancelSource != null)
                    {
                        traceCancelSource.Cancel();
                    }
                    else
                    {
                        try
                        {
                            DeIceProto.SendReq(new DeIceFnReqReadRegs());
                        }
                        catch (Exception ex)
                        {
                            AppendMessage($"ERROR:Executing Stop\n{ ex.ToString() } ");
                            if (Regs != null)
                                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                        }
                    }

                },
                o =>
                {
                    return (Regs?.IsRunning ?? false) || (traceCancelSource != null);
                },
                "Stop",
                Command_Exception
            );
            CmdRefresh = new RelayCommand(
                o =>
                {
                    RefreshDisassembly();

                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Refresh",
                Command_Exception
            );
            CmdDisassembleAt = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Disassemble At";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        DisassembleAt(dlg.Address);

                        //Not sure how to best pass this up to the UI
                        if (MainWindow is not null)
                        {
                            var i = MainWindow.ucDisAss.lbLines.Items.OfType<DisassItemOpModel>().Where(x => x.Address.Equals(dlg.Address)).FirstOrDefault();
                            MainWindow.ucDisAss.lbLines.ScrollIntoView(i);
                        }
                    }


                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Disassemble At",
                Command_Exception
            );

            CmdBreakpoints_Add = new RelayCommand(
                o =>
                {
                    var dlg = new DlgDumpMem(this);
                    dlg.Title = "Add breakpoint at";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        AddBreakpoint(dlg.Address);
                    }

                },
                o => true,
                "Add Breakpoint...",
                Command_Exception
                );

            CmdBreakpoints_Delete = new RelayCommand(
                o =>
                {
                    Breakpoints.Where(o => o.Selected).ToList().ForEach(o => RemoveBreakpoint(o.Address));
                },
                o => Breakpoints.Where(o => o.Selected).Any(),
                "Add Breakpoint...",
                Command_Exception
            );

            CmdWatches_Add = new RelayCommand(
                o =>
                {
                    var dlg = new DlgAddWatch(this);
                    dlg.Title = "Add watch";
                    if (MainWindow is not null)
                        dlg.Owner = MainWindow;

                    if (dlg.ShowDialog() == true)
                    {
                        Watches.Add(new WatchModel(dlg.Address, dlg.Symbol, dlg.WatchType, dlg.Indices));
                    }

                },
                o => true,
                "Add Breakpoint...",
                Command_Exception
                );

            CmdWatches_Delete = new RelayCommand(
                o =>
                {
                    Watches.Where(o => o.Selected).ToList().ForEach(o => RemoveWatch(o.Address));
                },
                o => Watches.Where(o => o.Selected).Any(),
                "Add Breakpoint...",
                Command_Exception
            );

            CmdLoadBinary = new RelayCommand(
                o =>
                {
                    var dlg = new DlgLoadBinary(this);
                    dlg.Owner = MainWindow;
                    dlg.Address = LoadBinaryAddr_last;
                    dlg.FileName = LoadBinaryFilename_last;
                    if (dlg.ShowDialog() == true)
                    {
                        LoadBinaryFile(dlg.Address, dlg.FileName);
                    }
                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Load Binary",
                Command_Exception
            );

            CmdSaveBinary = new RelayCommand(
                o =>
                {
                    var dlg = new DlgSaveBinary(this);
                    dlg.Owner = MainWindow;
                    dlg.Address = SaveBinaryAddr_last;
                    dlg.Length = SaveBinaryLength_last;
                    dlg.FileName = SaveBinaryFilename_last;
                    if (dlg.ShowDialog() == true)
                    {
                        SaveBinaryFile(dlg.Address, dlg.Length, dlg.FileName);
                    }
                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Load Binary",
                Command_Exception
            );

            CmdRunScript = new RelayCommand(
                o =>
                {
                    var dlg = new OpenFileDialog();
                    dlg.Filter = "Script files|*.noi|All files|*.*";
                    dlg.CheckFileExists = true;
                    dlg.CheckPathExists = true;
                    dlg.FilterIndex = 1;
                    if (dlg.ShowDialog(MainWindow) == true)
                    {
                        ReadCommandFile(dlg.FileName);
                    }
                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Load Binary",
                Command_Exception
            );

            CmdOpenAScript = new RelayCommand(
                o =>
                {
                    ReadCommandFile((string)o);
                },
                o =>
                {
                    return Regs?.IsStopped ?? false;
                },
                "Load Binary",
                Command_Exception
            );

            CmdClearLog = new RelayCommand(
                o =>
                {
                    Messages.Clear();
                },
                o =>
                {
                    return Messages.Any();
                },
                "Clear Message Log",
                Command_Exception
            );


            _deIceProto = new DeIceProtocolMain(Serial);

            DeIceProto.CommError += (o, e) =>
            {
                DoBeginInvoke(() => { AppendMessage($"ERROR:{ e.Exception }"); });
            };
            DeIceProto.OobDataReceived += (o, e) =>
            {
                DoBeginInvoke(() => { AppendMessage($"OOB:{ e.Data }"); });
            };
            DeIceProto.FunctionReceived += (o, e) =>
            {
                DoBeginInvoke(() =>
                {
                    AppendMessage($"FN:{e.Function.FunctionCode:X02} : { e.Function.GetType().Name }");

                    if (Regs == null)
                    {
                        // Refresh host status
                        try
                        {
                            DebugHostStatus = DeIceProto.SendReqExpectReply<DeIceFnReplyGetStatus>(new DeIceFnReqGetStatus());
                        } catch (Exception ex) {
                            Messages.Add($"Error: Trying to get Host Status:{ex.Message} ");
                        }
                    }

                    var x = e.Function as DeIceFnReplyRegsBase;
                    if (x != null && Regs != null)
                    {
                        Regs.FromDeIceProtocolRegData(x.RegisterData);

                        if (!RunFinish(true))
                        {
                            // breakpoint hit but it returned false...carry on
                            ReExecCurBreakpoint();

                            DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); // ignore response: TODO: check?
                            ApplyBreakpoints();

                            DeIceProto.SendReq(new DeIceFnReqRun());
                            Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;

                        }

                        //this is a horrid bodge for when get stuck it really shouldn't be "running" here!?
                        if (Regs.TargetStatus == 0)
                            Regs.TargetStatus = DeIceProtoConstants.TS_TRACE;

                    }
                });
            };

            if (hostStatus != null)
                UpdateHostStatus(hostStatus);

        }

        public bool Init()
        {
            //try and give it a tickle to see if its awake!
            try
            {
                DeIceProto.Flush();
                DebugHostStatus = DeIceProto.SendReqExpectReply<DeIceFnReplyGetStatus>(new DeIceFnReqGetStatus());
                var r = DeIceProto.SendReqExpectReply<DeIceFnReplyRegsBase>(new DeIceFnReqReadRegs());
                Regs?.FromDeIceProtocolRegData(r.RegisterData);
                RunFinish(false);
                return true;
            }
            catch (Exception ex)
            {
                if (Regs != null)
                {
                    Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                }
                Messages.Add($"Error {ex.Message}");
                return false;
            }

        }

        public void Command_Exception(object sender, ExceptionEventArgs args)
        {
            if (sender is RelayCommand)
            {
                Messages.Add($"Error in {(sender as RelayCommand).Name}:{args.Exception.Message}");
            }
            else
            {
                Messages.Add($"Error {args.Exception.Message}");
            }
            Messages.Add(args.Exception.ToString());
        }

        //TODO: Check if there's a better way of doing this
        void DoInvoke(Action a)
        {
            if (MainWindow is not null)
                MainWindow.Dispatcher.Invoke(a);
            else
                a.Invoke();
        }

        void DoBeginInvoke(Action a)
        {
            if (MainWindow is not null)
                MainWindow.Dispatcher.BeginInvoke(a);
            else
                a.Invoke();
        }

        /// <summary>
        /// when running this contains the list of breakpoints that have actually been sucessfully set
        /// </summary>
        private List<BreakpointModel> _activeBreakpoints = new List<BreakpointModel>();


        /// <summary>
        /// Execute a single instruction
        /// </summary>
        /// <returns>The registers after executing the instruction or null if failed to step</returns>
        protected DeIceFnReplyRun? Exec_SingleStep()
        {
            if (Regs.CanTrace)
            {

                Regs.SetTrace(true);
                DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); //ignore TODO: check?
                ApplyBreakpoints();
                return DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun());
            }
            else if (Regs is IRegisterSetPredictNext)
            {
                var ir = Regs as IRegisterSetPredictNext;
                if (ir != null)
                {
                    byte[] pdata = new byte[ir.PredictProgramDataSize];
                    var l = DisassMemBlock.Read(pdata, Regs.PCValue, ir.PredictProgramDataSize);
                    if (l >= ir.PredictProgramDataSize)
                    {
                        var nextPC = ir.PredictNext(pdata);

                        DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); //ignore TODO: check?
                        ApplyBreakpoints(nextPC);
                        return DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun());
                    }
                }
            }
            return null;
        }

        protected bool ReExecCurBreakpoint()
        {
            // check to see if we are currently in a breakpoint and the PC is pointing just after the breakpoint instruction
            if (Regs?.TargetStatus == DeIceProtoConstants.TS_BP)
            {
                try
                {
                    BreakpointModel curbp = _activeBreakpoints.Concat(Breakpoints).Where(b => b.Address.Equals(Regs.PCValue)).FirstOrDefault();
                    if (curbp != null)
                    {
                        //remove the breakpoint from the active list
                        _activeBreakpoints.RemoveAll(b => b.Address.Equals(Regs.PCValue));

                        //restore the original instruction
                        DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(
                            new DeIceFnReqSetBytes()
                            {
                                Items = curbp.OldOP.Select((b, i) =>
                                    new DeIceFnReqSetBytes.DeIceSetBytesItem()
                                    {
                                        Address = (curbp.Address + i).DeIceAddress, Data = b
                                    }
                                ).ToArray()
                            }
                        );


                        if (Regs.CanTrace)
                        {
                            // Set Trace mode and execute
                            bool old = Regs.SetTrace(true);
                            DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() });
                            var regs = DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun() { });
                            Regs.FromDeIceProtocolRegData(regs.RegisterData);
                            // Restore Trace mode - TODO: What to do if the BP instruction affected the SR traceflag
                            Regs.SetTrace(old);
                        }
                        else
                        {
                            var ir = Regs as IRegisterSetPredictNext;
                            if (ir != null)
                            {
                                byte[] pdata = new byte[ir.PredictProgramDataSize];
                                var l = DisassMemBlock.Read(pdata, Regs.PCValue, ir.PredictProgramDataSize);
                                if (l >= ir.PredictProgramDataSize)
                                {
                                    var nextPC = ir.PredictNext(pdata);
                                    if (!nextPC.Equals(Regs.PCValue))
                                    {
                                        if (!_activeBreakpoints.Where(a => a.Address == nextPC && a.Enabled).Any())
                                        {
                                            //set a temp breakpoint
                                            ApplyBreakpointsInt(new[] { new BreakpointModel(this) { Address = nextPC, Enabled = true } });
                                        }
                                        DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); //ignore TODO: check?
                                        Regs.FromDeIceProtocolRegData(
                                            DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun()).RegisterData
                                        );
                                    }
                                }
                            }

                        }

                        return true;

                    }

                }
                catch (Exception ex)
                {
                    Messages.Add($"Unexpected error re-executing breakpoint:{ex.ToString()}");
                }
            }
            return false;
        }


        public void DoContinue()
        {
            if (Regs != null)
            {
                try
                {
                    ReExecCurBreakpoint();

                    Regs.SetTrace(false);
                    DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); // ignore responese: TODO: check?
                    ApplyBreakpoints();
                    DeIceProto.SendReq(new DeIceFnReqRun());
                }
                catch (Exception ex)
                {
                    AppendMessage($"ERROR:Executing Continue\n{ ex.ToString() } ");
                }
                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
            }

        }

        public DeIceFnReplySetBytes ApplyBreakpointsInt(IList<BreakpointModel> chunk)
        {
            var bpl = DebugHostStatus.BreakPointInstruction.Length;
            var req = Breakpoints2Req(chunk, true);
            var ret = DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(req);

            //TODO: recover more gracefully this will leave some breakpoints set and the host in an inconsistent state?
            if (ret.Data.Length != chunk.Count())
                throw new Exception($"Unexpected length returned from SetBytes expected {chunk.Count()} received {ret.Data.Length}");

            for (int i = 0; i < ret.Data.Length / bpl; i++)
            {
                var ob = new byte[bpl];
                Array.Copy(ret.Data, i * bpl, ob, 0, bpl);
                chunk[i].OldOP = ob;
            }
            _activeBreakpoints.AddRange(chunk.Take(ret.Data.Length));

            return ret;
        }


        /// <summary>
        /// Apply the breakpoints in <see cref="Breakpoints"/> to the host, if not already applied
        /// </summary>
        /// <param name="tempBp">If this is not null this address is also set as a temporary breakpoint</param>
        /// <exception cref="Exception">An unexpected reply was received from setBytes</exception>
        public void ApplyBreakpoints(DisassAddressBase? tempBp = null, bool reExecCurBP = true)
        {

            if (reExecCurBP)
                ReExecCurBreakpoint();

            // how many breakpoints we can fit in the buffer
            int MAXBP = (DebugHostStatus.ComBufSize / 8) - 2;

            IEnumerable<BreakpointModel> wantedBreakpoints;

            if (tempBp != null)
                wantedBreakpoints = new[] {new BreakpointModel(this) { Address = tempBp, Enabled = true }}.Concat(Breakpoints.Where(o => o.Address != tempBp));
            else
                wantedBreakpoints = Breakpoints;

            //get any Breakpoints not already applied
            var bp2a = wantedBreakpoints.Where(o => o.Enabled && !_activeBreakpoints.Where(a => a.Address.Equals(o.Address)).Any());

            while (bp2a.Any())
            {
                var chunk = bp2a.Take(MAXBP).ToArray();
                var ret = ApplyBreakpointsInt(chunk);
                bp2a = bp2a.Skip(ret.Data.Length);

                //if we got fewer back than we sent then it was dodgy memory, skip that in the list and set to disabled.
                if (ret.Data.Length < chunk.Length && bp2a.Any())
                {
                    var bbad = bp2a.First();
                    bbad.Enabled = false;
                    Messages.Add($"The breakpoint at {bbad.Address:X08} couldn't be set and was disabled");
                    bp2a = bp2a.Skip(1);
                }
            }


            //remove any that are now disabled or no longer in the Breakpoints list
            List<BreakpointModel> rembp = _activeBreakpoints.Where(a => a.Enabled == false || !wantedBreakpoints.Where(b => b.Address.Equals(a.Address)).Any()).ToList();
            UnApplyBreakpoints(rembp);
            rembp.ForEach(a => _activeBreakpoints.Remove(a));

        }

        DeIceFnReqSetBytes Breakpoints2Req(IEnumerable<BreakpointModel> breakpoints, bool apply)
        {
            if (apply)
            {
                return new DeIceFnReqSetBytes()
                {
                    Items = breakpoints.Select(
                    bp => DebugHostStatus.BreakPointInstruction.Select( (b,i) =>
                            new DeIceFnReqSetBytes.DeIceSetBytesItem()
                            {
                                Address = (bp.Address + i).DeIceAddress,
                                Data = b
                            }
                        )
                    ).SelectMany(o => o).ToArray()
                };
            }
            else
            {
                return new DeIceFnReqSetBytes()
                {
                    Items = breakpoints.Select(
                        bp => bp.OldOP.Select( (b, i) =>
                            new DeIceFnReqSetBytes.DeIceSetBytesItem()
                            {
                                Address = (bp.Address + i).DeIceAddress,
                                Data = b
                            }
                        )
                    ).SelectMany(o => o).ToArray()
                };

            }
        }


        public void UnApplyBreakpoints(IEnumerable<BreakpointModel> items)
        {
            var bpl = DebugHostStatus.BreakPointInstruction.Length;

            // how many breakpoints we can fit in the buffer
            int MAXBP = (DebugHostStatus.ComBufSize / 8) - 2;
            while (items.Any())
            {
                var chunk = items.Take(MAXBP).ToArray();
                var req = Breakpoints2Req(chunk, false);
                var ret = DeIceProto.SendReqExpectReply<DeIceFnReplySetBytes>(req);


                for (int i = 0; i < chunk.Length && bpl * i < ret.Data.Length; i++)
                {
                    byte[] r = new byte[bpl];
                    Array.Copy(ret.Data, i * bpl, r, 0, bpl);
                    if (!r.SequenceEqual(DebugHostStatus.BreakPointInstruction))
                    {
                        var bb = chunk[i];
                        Messages.Add($"WARNING: breakpoint at {bb.Address:X08} could not be reset, code is in an unexpected state. Found={BitConverter.ToString(r)}, expected={BitConverter.ToString(DebugHostStatus.BreakPointInstruction)}");
                    }
                }

                items = items.Skip(ret.Data.Length / bpl);

                //if we got fewer back than we sent then it was dodgy memory, skip that in the list and set to disabled.
                if (ret.Data.Length < chunk.Length && _activeBreakpoints.Any())
                {
                    var bbad = items.First();
                    bbad.Enabled = false;
                    Messages.Add($"WARNING: breakpoint at {bbad.Address:X08} could not be reset, code is in an unexpected state. The memory was not writeable!");
                    items.Skip(1);
                }

            }
        }

        /// <summary>
        /// Run has finished (or initial read regs reply received) update the display, registers should already have been updated
        /// </summary>
        /// <returns>If this is a breakpoint the condition is checked and returned here, otherwise return false</returns>
        public bool RunFinish(bool unApplyBreakpoints = true)
        {
            bool ret = Regs.TargetStatus != DeIceProtoConstants.TS_BP;
            try
            {
                //undo old breakpoints
                if (unApplyBreakpoints)
                {
                    UnApplyBreakpoints(_activeBreakpoints);
                    _activeBreakpoints.Clear();
                }

                if (_debugHostStatus == null)
                {
                    DebugHostStatus = DeIceProto.SendReqExpectReply<DeIceFnReplyGetStatus>(new DeIceFnReqGetStatus());
                }

                DisassembleAt(Regs.PCValue);

                foreach (var w in Watches)
                {
                    byte[] buf = new byte[w.DataSize];
                    DeIceProto.ReadMemBlock(w.Address.DeIceAddress, buf, 0, (int)w.DataSize);
                    w.Data = buf;
                }

                if (Regs?.TargetStatus == DeIceProtoConstants.TS_BP)
                {
                    foreach (var curbp in Breakpoints.Where(b => b.Address.Equals(Regs.PCValue)))
                    {
                        if (curbp != null && curbp.Enabled)
                        {
                            if (curbp.ConditionCode != null)
                            {
                                if (curbp.ConditionCode.Execute())
                                    ret = true;
                                else
                                    Messages.Add($"Encountered breakpoint at {curbp.Address:X08} [{curbp.SymbolStr ?? ""}] {((!ret) ? " - skipped" : "")}");
                            }
                            else
                            {
                                ret = true;
                            }
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                AppendMessage($"ERROR:reading memory\n{ ex.ToString() } ");
                if (Regs != null)
                    Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
            }
            return ret;
        }

        public void DisassembleAt(DisassAddressBase addr)
        {
            //check to see if pc is in the current DisassMemBlock, if not either extend or load new
            if (DisassMemBlock == null || DisassMemBlock.BaseAddress > addr || DisassMemBlock.EndPoint <= addr + 64)
            {
                Int64 block_offset;
                if (DisassMemBlock != null && (block_offset = addr - DisassMemBlock.EndPoint) > 0 &&  block_offset < 1024)
                {
                    //close just extend until we're in range
                    DisassMemBlock.MorePlease((uint)(block_offset + 512));
                }
                else
                {
                    var disdat = new byte[1024];
                    DeIceProto.ReadMemBlock(addr.DeIceAddress, disdat, 0, 1024);

                    DisassMemBlock = new DisassMemBlock(this, addr, disdat, GetDisass(), Regs.DisassState);
                }
            }

            //in range just update pc
            DisassMemBlock.PC = Regs?.PCValue ?? DisassAddressBase.Empty;

        }

        public void TraceTo(DisassAddressBase addr)
        {
            if (Regs == null)
                return;

            traceCancelSource = new CancellationTokenSource();
            CancellationToken cancellationToken = traceCancelSource.Token;

            Task.Run(() =>
            {
                try
                {
                    byte lastTargetStatus = Regs.TargetStatus;
                    try
                    {
                        if (Regs.CanTrace)
                        {
                            Regs.SetTrace(true);
                            DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); //ignore TODO: check?

                            ApplyBreakpoints();

                            while (Regs.PCValue != addr && !cancellationToken.IsCancellationRequested)
                            {
                                var rr = DeIceProto.SendReqExpectReply<DeIceFnReplyRun>(new DeIceFnReqRun());
                                Regs.FromDeIceProtocolRegData(rr.RegisterData);
                                if (Regs.TargetStatus != DeIceProtoConstants.TS_TRACE)
                                {
                                    bool cont = false;
                                    DoInvoke(() => cont = RunFinish(true));
                                    if (cont && Regs.TargetStatus == DeIceProtoConstants.TS_BP)
                                    {
                                        break;
                                    }
                                }

                                lastTargetStatus = Regs.TargetStatus;
                                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;

                                //TODO: Move invoke inside runfinish where it is needed
                                DoInvoke(() => RunFinish(false));

                            }
                        } else if (Regs is IRegisterSetPredictNext)
                        {
                            DeIceProto.SendReqExpectStatusByte<DeIceFnReplyWriteRegs>(new DeIceFnReqWriteRegs() { RegData = Regs.ToDeIceProtcolRegData() }); //ignore TODO: check?

                            ApplyBreakpoints();

                            while (Regs.PCValue != addr && !cancellationToken.IsCancellationRequested)
                            {
                                var rr = Exec_SingleStep();
                                Regs.FromDeIceProtocolRegData(rr.RegisterData);
                                if (Regs.TargetStatus != DeIceProtoConstants.TS_TRACE)
                                {
                                    bool cont = false;
                                    DoInvoke(() => cont = RunFinish(true));
                                    if (cont && Regs.TargetStatus == DeIceProtoConstants.TS_BP)
                                    {
                                        break;
                                    }
                                }

                                lastTargetStatus = Regs.TargetStatus;
                                Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;

                                //TODO: Move invoke inside runfinish where it is needed
                                DoInvoke(() => RunFinish(false));

                            }

                        }
                    }
                    finally
                    {
                        Regs.TargetStatus = (byte)lastTargetStatus;
                    }
                }
                catch (Exception ex)
                {
                    DoInvoke(() =>
                    {
                        Messages.Add($"TRACE ERROR: {ex.Message}");
                        Messages.Add(ex.ToString());
                        Regs.TargetStatus = DeIceProtoConstants.TS_RUNNING;
                    });

                }
            }).ContinueWith((t) =>
            {
                traceCancelSource.Dispose();
                traceCancelSource = null;
            }); ;
        }

        public BreakpointModel AddBreakpoint(DisassAddressBase address)
        {
            int i = 0;
            while (i < _breakpointsint.Count && _breakpointsint[i].Address < address)
                i++;

            var ret = new BreakpointModel(this) { Address = address, Enabled = true };
            _breakpointsint.Insert(i, ret);
            return ret;
        }

        public void RemoveBreakpoint(DisassAddressBase address)
        {
            _breakpointsint.Where(o => o.Address == address).ToList().ForEach(o => _breakpointsint.Remove(o));
        }

        public void RemoveWatch(DisassAddressBase address)
        {
            _watches.Where(o => o.Address == address).ToList().ForEach(o => _watches.Remove(o));
        }

        public byte GetByte(DisassAddressBase addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr.DeIceAddress, Len = 1 });
            return r.Data[0];
        }

        public ushort GetWord(DisassAddressBase addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr.DeIceAddress, Len = 2 });
            return (ushort)((r.Data[0] << 8) | r.Data[1]);
        }

        public uint GetLong(DisassAddressBase addr)
        {
            var r = DeIceProto.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr.DeIceAddress, Len = 4 });
            return (ushort)((r.Data[0] << 8) | (r.Data[1] << 16) | (r.Data[2] << 8) | r.Data[3]);
        }


        void Regs_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterSetModelBase.IsStopped))
            {
                RaisePropertyChangedEvent(nameof(HostBusy));
            }

            if (e.PropertyName == nameof(RegisterSetModelBase.TargetStatus))
            {
                //really? should this be here?
                DoInvoke(() => CommandManager.InvalidateRequerySuggested());
            }

        }


        private void UpdateHostStatus(DeIceFnReplyGetStatus hostStatus)
        {
            _debugHostStatus = hostStatus;
            RaisePropertyChangedEvent(nameof(DebugHostStatus));

            Messages.Add($"HOSTSTATUS:{hostStatus.TargetName}");

            bool changed = false;

            // check the right sort of register model is present
            if (DebugHostStatus.ProcessorType == DeIceProtoConstants.HOST_68k && Regs?.GetType() != typeof(RegisterSetModel68k))
            {
                if (Regs != null)
                    Regs.PropertyChanged -= Regs_PropertyChanged;

                _regs = new RegisterSetModel68k(this);
                changed = true;
            } 
            else if (DebugHostStatus.ProcessorType == DeIceProtoConstants.HOST_x86_186 && Regs?.GetType() != typeof(RegisterSetModelx86_16))
            {
                if (_regs != null)
                    _regs.PropertyChanged -= Regs_PropertyChanged;

                _regs = new RegisterSetModelx86_16(this);
                changed = true;
            }
            else if (DebugHostStatus.ProcessorType == DeIceProtoConstants.HOST_x86_386 && Regs?.GetType() != typeof(RegisterSetModelx86_386))
            {
                if (_regs != null)
                    _regs.PropertyChanged -= Regs_PropertyChanged;

                _regs = new RegisterSetModelx86_386(this);
                changed = true;
            }
            else if (DebugHostStatus.ProcessorType == DeIceProtoConstants.HOST_ARM2 && Regs?.GetType() != typeof(RegisterSetModelArm2))
            {
                if (_regs != null)
                    _regs.PropertyChanged -= Regs_PropertyChanged;

                _regs = new RegisterSetModelArm2(this);
                changed = true;
            }
            else if (DebugHostStatus.ProcessorType == DeIceProtoConstants.HOST_65816 && Regs?.GetType() != typeof(RegisterSetModel65816))
            {
                if (_regs != null)
                    _regs.PropertyChanged -= Regs_PropertyChanged;

                _regs = new RegisterSetModel65816(this);
                changed = true;
            }

            if (changed)
            {
                _regs.PropertyChanged += Regs_PropertyChanged;
                if (!_sampleData)
                    Regs.FromDeIceProtocolRegData(
                        DeIceProto.SendReqExpectReply<DeIceFnReplyReadRegs>(new DeIceFnReqReadRegs()).RegisterData
                        );
                RaisePropertyChangedEvent(nameof(Regs));

            }

            try
            {
                RefreshDisassembly();
            }
            catch (Exception) { }
        }

        public IDisAss GetDisass()
        {
            //TODO: work out from DebugHostType
            if (_debugHostStatus?.ProcessorType == DeIceProtoConstants.HOST_ARM2)
                return new DisassArm.DisassArm();
            else if (_debugHostStatus?.ProcessorType == DeIceProtoConstants.HOST_68k)
                return new Disass68k.Disass68k();
            else if (_debugHostStatus?.ProcessorType == DeIceProtoConstants.HOST_x86_186)
                return new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_186);
            else if (_debugHostStatus?.ProcessorType == DeIceProtoConstants.HOST_x86_386)
                return new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_386);
            else if (_debugHostStatus?.ProcessorType == DeIceProtoConstants.HOST_65816)
                return new Disass65816.Disass65816();
            else
                throw new NotImplementedException($"Unknown host type {_debugHostStatus?.ProcessorType}");

        }

        private void RefreshDisassembly()
        {
            if (Regs?.IsStopped ?? false)
            {
                int len = DisassMemBlock?.Data?.Length ?? 128;
                DisassAddressBase st = DisassMemBlock?.BaseAddress ?? Regs.PCValue;

                var disdat = new byte[len];
                DeIceProto.ReadMemBlock(st.DeIceAddress, disdat, 0, len);

                DisassMemBlock = new DisassMemBlock(this, st, disdat, GetDisass(), Regs.DisassState);
                DisassMemBlock.PC = Regs.PCValue;
            }

        }
    }
}
