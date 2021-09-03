using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DeIce68k.ViewModel.Scripts
{
    public abstract class ScriptBase : IDisposable
    {
        private DeIceAppModel _app;

        public class MessagesTextWriter : TextWriter
        {
            ScriptBase _base;

            private StringBuilder buf = new StringBuilder();

            public override void Write(char value)
            {
                if (value == '\r' || value == '\n')
                {
                    Flush();
                } else
                {
                    buf.Append(value);
                }

                base.Write(value);
            }

            public override void Flush()
            {
                if (buf.Length != 0)
                {
                    _base._app.Messages.Add(buf.ToString());
                    buf.Clear();
                }
                base.Flush();
            }

            public override Encoding Encoding => Encoding.UTF8;

            public MessagesTextWriter(ScriptBase b)
            {
                _base = b;
            }
        }

        MessagesTextWriter _messages;
        private bool disposedValue;

        public uint GetSymbol(string s)
        {
            uint ret;
            if (_app.Symbols.SymbolToAddress(s, out ret))
                return ret;
            else
                throw new ArgumentException($"Symbol {s} is not defined");
        }

        public byte GetByte(uint addr)
        {
            return _app.GetByte(addr);
        }

        public ushort GetWord(uint addr)
        {
            return _app.GetWord(addr);
        }

        public uint GetLong(uint addr)
        {
            return _app.GetLong(addr);
        }

        public byte GetByte(string s)
        {
            return GetByte(GetSymbol(s));
        }

        public ushort GetWord(string s)
        {
            return GetWord(GetSymbol(s));
        }

        public uint GetLong(string s)
        {
            return GetLong(GetSymbol(s));
        }


        protected RegisterSetModel Registers { get => _app.Regs; }

        protected MessagesTextWriter Messages { get => _messages; }

        public ScriptBase(DeIceAppModel app, string orgCode)
        {
            OrgCode = orgCode;
            _app = app;
            _messages = new MessagesTextWriter(this);
        
        }

        public string OrgCode { get; init; }
        public abstract bool Execute();
              

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _messages.Flush();
                    _messages.Dispose();
                }

                disposedValue = true;
            }
        }


        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
