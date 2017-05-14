using System.Collections.ObjectModel;

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
