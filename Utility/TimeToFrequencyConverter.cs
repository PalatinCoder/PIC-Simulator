using System;
using Windows.UI.Xaml.Data;

namespace PIC_Simulator
{
    /// <summary>
    /// Dieser Konverter wandelt die Quarzfrequenz in die
    /// Instruction Time um.
    /// </summary>
    public class TimeToFrequencyConverter : IValueConverter
    {
        /// <summary>
        /// Frequenz in TimeSpan (MHz in ns)
        /// </summary>
        /// <param name="value">f in MHz</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns>System.TimeSpan Instruction Cycle Time</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(TimeSpan)) throw new InvalidOperationException("The target must be of type TimeSpan");
            if (!double.TryParse((string)value, out double f_QuarzMHz)) throw new InvalidOperationException("The value must be a number");

            double f_Quarz = f_QuarzMHz * 1000000;
            double f_PIC = f_Quarz / 4;
            double T_PIC = 1 / f_PIC;
            long nanoseconds = (long)(T_PIC * 1e9);
            return new TimeSpan(nanoseconds / 100); // 1 Tick ^= 100 ns
        }

        /// <summary>
        /// TimeSpan in Frequenz (ns in MHz)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(string)) throw new InvalidOperationException("The target must be of type string");
            if (!(value is TimeSpan)) throw new InvalidOperationException("Value must be of type TimeSpan");

            long ticks = ((TimeSpan)value).Ticks;
            double T_PIC = ticks * 100 * 1e-9;
            double f_Quarz = 4 * (1 / T_PIC);
            double f_QuarzMHz = f_Quarz / 1e6;

            return f_QuarzMHz.ToString();
        }
    }
}
