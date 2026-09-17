using System.Globalization;
using System.Windows.Controls;

namespace TallyPrimeConnector.App;

public sealed class IndianDatePicker : DatePicker
{
    public IndianDatePicker()
    {
        SelectedDateFormat = DatePickerFormat.Short;
        SelectedDateChanged += (_, _) => ApplyIndianDateFormat();
        CalendarClosed += (_, _) => ApplyIndianDateFormat();
    }

    private void ApplyIndianDateFormat()
    {
        Text = SelectedDate?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
