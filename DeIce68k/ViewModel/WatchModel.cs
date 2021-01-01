using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disass68k;
using System.IO;

namespace DeIce68k.ViewModel
{
    public enum WatchType {
        Empty = 0,
        X08 = 0x0001,
        X16 = 0x0002,
        X24 = 0x0003,
        X32 = 0x0004,
        X64 = 0x0008
    };

    public static class WatchType_Ext
    {
        public static int Size(this WatchType w)
        {
            return (int)w & 0x0F;
        }

        public static string ValueString(this WatchType w, BinaryReader b)
        {
            StringBuilder sb = new StringBuilder();
            //assumes big endian
            for (int i = 0; i < w.Size(); i++)
            {
                sb.Append($"{b.ReadByte():X2}");
            }

            return sb.ToString();
        }

        public static WatchType StringToWatchType(string s)
        {
            foreach (var w in Enum.GetValues(typeof(WatchType)).Cast<WatchType>())
            {
                if (String.Compare(s, w.ToString(), true) == 0)
                    return w;
            }
            return WatchType.Empty; 
        }
    }

    public class WatchModel : ObservableObject
    {
        private string _name;
        public string Name
        {
            get { return _name; }
            set {
                _name = value;
                RaisePropertyChangedEvent(nameof(Name));
                RaisePropertyChangedEvent(nameof(FullName));
            }
        }

        /// <summary>
        /// Name with dimensions
        /// </summary>
        public string FullName
        {
            get
            {
                string n = (string.IsNullOrEmpty(Name)) ? $"[{Address:X8}]" : Name;
                return (Dimensions == null || Dimensions.Length == 0)
                    ? n
                    : n + string.Join("", Dimensions.Select(x => $"[{x}]"));                               
            }
        }

        public uint Address { get; }

        public WatchType WatchType { get; }

        public int[] Dimensions { get; }

        private byte[] _data;
        public byte[] Data
        {
            get {
                return _data;
            }
            set {
                CheckType(value);
                _data = value;
                RaisePropertyChangedEvent(nameof(Data));
                RaisePropertyChangedEvent(nameof(ValueString));
            }
        }

        public WatchModel(uint address, string name, WatchType type, int [] dimensions)
        {
            Address = address;
            Name = name;
            WatchType = type;
            Dimensions = dimensions;
        }

        private void CheckType(byte [] data)
        {
            if (data == null)
                return;
            if (data.Length == 0)
                return;

            if (data.Length != DataSize)
                throw new ArgumentException($"Error setting watch expected {DataSize} bytes got {data.Length}");
        }

        public int DataSize {
            get
            {
                return (Dimensions == null || Dimensions.Length == 0)
                    ? WatchType.Size()
                    : Dimensions.Aggregate((a, x) => a * x) * WatchType.Size();
            }
        }
     
        public string ValueString
        {
            get
            {
                if (Data == null || Data.Length == 0)
                    return "?";

                StringBuilder sb = new StringBuilder();
                try
                {
                    using (var m = new MemoryStream(Data))
                    using (var b = new BinaryReader(m))
                    {
                        if (Dimensions == null || Dimensions.Length == 0)
                        {
                            sb.Append(WatchType.ValueString(b));
                        }
                        else if (Dimensions.Length > 2)
                        {
                            int[] indeces = new int[Dimensions.Length - 2];

                            bool ok = true;
                            while (ok)
                            {
                                for (int i = 0; i < indeces.Length; i++)
                                    sb.Append($"[{indeces[i]}]");
                                sb.AppendLine("[][]={");
                                Do2Dim(sb, b);
                                sb.AppendLine("}");

                                int j = indeces.Length - 1;
                                while (j >= 0)
                                {
                                    indeces[j]++;
                                    if (indeces[j] < Dimensions[j])
                                        break;
                                    j--;
                                }

                                if (j < 0)
                                    ok = false;
                            }
                        }
                        else if (Dimensions.Length == 2)
                        {
                            Do2Dim(sb, b);
                        }
                        else
                        {
                            Do1Dim(sb, b);
                        }
                    }
                } catch (Exception ex)
                {
                    sb.Append(ex.ToString());
                }

                return sb.ToString();
            }
        }

        private void Do1Dim(StringBuilder sb, BinaryReader b)
        {
            for (int i = 0; i < Dimensions[Dimensions.Length-1]; i++)
            {
                if (i != 0)
                    sb.Append(", ");
                sb.Append(WatchType.ValueString(b));
            }
            sb.AppendLine();
        }

        private void Do2Dim(StringBuilder sb, BinaryReader b)
        {
            for (int i = 0; i < Dimensions[Dimensions.Length-2]; i++)
            {
                Do1Dim(sb, b);
            }
        }

    }
}
