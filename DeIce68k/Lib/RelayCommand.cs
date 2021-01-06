using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DeIce68k.Lib
{
    public class RelayCommand<T> : ICommand where T : class
    {
        private Action<T> execute = null;
        private Predicate<T> canExecute = null;


        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            try
            {
                return canExecute(parameter as T);
            } catch (Exception)
            {
                return false;
            }
        }

        public void Execute(object parameter)
        {
            try
            {
                execute(parameter as T);
            } catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    $"An error occurred:{ex.Message}",
                    MessageBoxButton.OK
                    );
            }

        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute) =>
            (this.execute, this.canExecute) = (execute, canExecute);
    }
}
