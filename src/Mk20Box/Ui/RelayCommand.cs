using System;
using System.Windows.Input;

namespace Mk20Box.Ui
{
    /// <summary>Command wrapper for simple button bindings.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action execute;

        public RelayCommand(Action execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            execute();
        }
    }
}
