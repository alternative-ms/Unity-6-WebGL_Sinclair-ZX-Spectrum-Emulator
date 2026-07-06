// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace CSharp.Core.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Recursively raise change events for all properties.
        /// </summary>
        protected void RaiseAllPropertiesChanged()
        {
            foreach (var propertyInfo in GetType().GetProperties())
            {
                // Check if the property is of type ViewModelBase or inherits from it
                if (propertyInfo.PropertyType.IsSubclassOf(typeof(ViewModelBase)) || propertyInfo.PropertyType == typeof(ViewModelBase))
                {
                    // Get the value of the property (which should be a ViewModelBase instance)
                    var viewModelBaseProperty = propertyInfo.GetValue(this) as ViewModelBase;

                    // Call RaiseAllPropertiesChanged on the property if it's not null
                    viewModelBaseProperty?.RaiseAllPropertiesChanged();
                }

                OnPropertyChanged(propertyInfo.Name);
            }
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
