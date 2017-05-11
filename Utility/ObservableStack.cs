using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PIC_Simulator
{
    public class ObservableStack<T> : ObservableCollection<T>
    {
        public void Push(T item)
        {
                base.Add(item);
        }

        public T Pop()
        {
            T item = base[base.Count - 1];
            base.Remove(item);
            return item;
        }
    }
}
