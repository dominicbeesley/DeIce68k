using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeIce68k.ViewModel
{
    public class DeIceAppModelDataContext: DependencyObject
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(@"ViewModel",
        typeof(DeIceAppModel), typeof(DeIceAppModelDataContext),
        new PropertyMetadata
        {
            DefaultValue = null,
            PropertyChangedCallback = new PropertyChangedCallback(DeIceAppModelDataContext.ViewModelPropertyChanged)
        });

        public DeIceAppModel ViewModel
        {
            get { return (DeIceAppModel)this.GetValue(DeIceAppModelDataContext.ViewModelProperty); }
            set { this.SetValue(DeIceAppModelDataContext.ViewModelProperty, value); }
        }

        private static void ViewModelPropertyChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
        }
    }

    public class AddrTextValidation : ValidationRule
    {
        public DeIceAppModelDataContext Context { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return new ValidationResult(true, null);
        }

    }
}
