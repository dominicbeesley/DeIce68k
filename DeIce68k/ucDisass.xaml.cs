using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Disass68k;
using DeIce68k.ViewModel;
using System.IO;
using System.Text;
using System.Linq;
using DeIce68k.Lib;
using DisassShared;

namespace DeIce68k
{

    public class DisassTemplateSelector : DataTemplateSelector
    {
        public DataTemplate OpModelTemplate { get; set; }
        public DataTemplate LabelTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DisassItemLabelModel)
                return LabelTemplate;
            else
                return OpModelTemplate;
            
        }
    }

    public class OperandTemplateSelector : DataTemplateSelector
    {
        public DataTemplate String { get; set; }
        public DataTemplate Symbol { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DisRec2OperString_String)
                return String;
            if (item is DisRec2OperString_Symbol)
                return Symbol;
            else
                return null;

        }
    }


    /// <summary>
    /// Interaction logic for ucDisass.xaml
    /// </summary>
    public partial class ucDisass : UserControl
    {
        public ucDisass()
        {
            InitializeComponent();
            var sv = lbLines.GetDescendantByType<ScrollViewer>();


            sv.ScrollChanged += ListBox_ScrollChanged;
        }

        private void ListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var sv = (ScrollViewer)sender;

            if (e.ExtentHeightChange == 0)
            {
                if ((sv.ScrollableHeight <= sv.VerticalOffset)
                && (sv.ScrollableHeight != 0.0 && sv.VerticalOffset != 0.0))
                {
                    try
                    {
                        (DataContext as DisassMemBlock)?.MorePlease();
                    } catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                if (e.VerticalChange > 0 && sv.VerticalOffset > e.VerticalChange)
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - e.VerticalChange);
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var om = e.OldValue as DisassMemBlock;
            if (om != null)
                om.PropertyChanged -= Dm_PropertyChanged;

            var dm = e.NewValue as DisassMemBlock;
            if (dm != null)
                dm.PropertyChanged += Dm_PropertyChanged;
        }

        private void Dm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var dm = lbLines.DataContext as DisassMemBlock;

            if (e.PropertyName == nameof(DisassMemBlock.PC) && dm != null)
            {
                //find current pc item (if in block) and scroll into view
                var pci = lbLines.Items.OfType<DisassItemOpModel>().Where(x => x.PC).FirstOrDefault();
                if (pci != null)
                {
                    lbLines.ScrollIntoView(pci);
                }
            }

        }
        

        private void LbLines_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void btnBreakpoint_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.DataContext as DisassItemOpModel;
            if (item == null)
                return;

            if (item.IsBreakpoint && !item.IsBreakpointEnabled)
                item.Parent.Breakpoints.Where(o => o.Address == item.Address).ToList().ForEach(b => b.Enabled = true);
            else if (item.IsBreakpoint)
                item.Parent.RemoveBreakpoint(item.Address);
            else
                item.Parent.AddBreakpoint(item.Address);
        }
    }
}
