using Avalonia.Controls;
using Avalonia.Interactivity;
using framenion.Src;
using System.Threading.Tasks;

namespace framenion;

public partial class MessageBox : Window
{
	public string? Message { get; }
	public bool ShowCancelButton { get; }
	public string OkButtonText { get; } = "OK";
	public string CancelButtonText { get; } = "Cancel";

	public MessageBox()
	{
		InitializeComponent();
		DataContext = this;
	}

	public MessageBox(string message, string title, bool showCancelButton = false, string okButtonText = "OK", string cancelButtonText = "Cancel")
	{
		Message = message;
		ShowCancelButton = showCancelButton;
		OkButtonText = okButtonText;
		CancelButtonText = cancelButtonText;
		InitializeComponent();
		DataContext = this;
		Title = title;
	}

	public static void Show(string title, string message)
	{
		var msgBox = new MessageBox(message, title, showCancelButton: false);
		var owner = AppData.MainWindow;
		if (owner != null && owner.IsVisible) {
			_ = msgBox.ShowDialog(owner);
		} else {
			msgBox.Show();
		}
	}

	public static async Task<bool> AskYesNo(string title, string message, string okButtonText = "Yes", string cancelButtonText = "No")
	{
		var msgBox = new MessageBox(message, title, showCancelButton: true, okButtonText: okButtonText, cancelButtonText: cancelButtonText);
		var owner = AppData.MainWindow;
		if (owner != null && owner.IsVisible) {
			return (await msgBox.ShowDialog<bool?>(owner)) ?? false;
		}
		return false;
	}

	private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

	private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}