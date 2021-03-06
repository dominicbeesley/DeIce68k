﻿using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace DossySerialPort
{
    public class DossySerial : IDossySerial, IDisposable
    {
        object syncObj = new object();

        SerialPort _port;
        byte peekedbyte = 0;
        bool peeked = false;

        public DossySerial(string portname, int baudrate)
        {
            _port = new SerialPort(portname, baudrate, Parity.None, 8, StopBits.One);
            _port.DataReceived += (o, e) => { OnDataReceived(); };


            _port.Open();

            _port.BreakState = false;
            _port.Handshake = Handshake.None;
            _port.RtsEnable = true;
            _port.DtrEnable = true;
        }

        private void OnDataReceived()
        {
            if (DataReceived is not null)
            {
                DataReceived(this, EventArgs.Empty);
            }
        }


        #region IDossySerial

        public int Available => _port.BytesToRead;

        public event EventHandler DataReceived;


        public int Read(byte[] b, int length, int timeoutms = 0)
        {
            _port.ReadTimeout = timeoutms;
            int ptr = 0;

            if (length > 0)
            {
                lock (syncObj)
                {
                    if (peeked)
                    {
                        b[0] = peekedbyte;
                        peeked = false;
                        ptr++;
                        length--;
                    }
                }

                while (length > 0)
                {
                    int l = _port.Read(b, ptr, length);
                    ptr += l;
                    length -= l;
                }
            }

            return ptr;

        }

        public void Write(byte[] b, int offset, int length, int timeoutms = 0)
        {
            _port.WriteTimeout = timeoutms;
            _port.Write(b, offset, length);

        }

        public byte PeekByte(int timeoutms = 0)
        {
            lock(syncObj)
            {
                if (this.peeked)
                {
                    return peekedbyte;
                }
            }

            _port.ReadTimeout = timeoutms;
            int ret = _port.ReadByte();
            if (ret == -1)
                throw new EndOfStreamException();
            lock(syncObj)
            {
                this.peeked = true;
                this.peekedbyte = (byte)ret;
            }
            return (byte)ret;
        }

        public byte ReadByte(int timeoutms = 0)
        {
            lock (syncObj)
            {
                if (this.peeked)
                {
                    this.peeked = false;
                    return peekedbyte;
                }
            }

            _port.ReadTimeout = timeoutms;
            int ret = _port.ReadByte();
            if (ret == -1)
                throw new EndOfStreamException();
            return (byte)ret;
        }


        #endregion

        #region Disposable

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DossySerial()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        #endregion
    }
}
