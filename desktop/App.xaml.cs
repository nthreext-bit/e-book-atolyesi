using System.Windows;
using System.Globalization;
using System.Threading;
using KitaplikEditoru.Infrastructure;
using KitaplikEditoru.Localization;

namespace KitaplikEditoru;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		var culture = new CultureInfo("tr-TR");
		Thread.CurrentThread.CurrentCulture = culture;
		Thread.CurrentThread.CurrentUICulture = culture;
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		var resources = Strings.ResourceManager
			.GetResourceSet(CultureInfo.CurrentUICulture, createIfNotExists: true, tryParents: true);
		if (resources is not null)
		{
			foreach (System.Collections.DictionaryEntry entry in resources)
			{
				Resources[entry.Key.ToString()!] = entry.Value;
			}
		}

		base.OnStartup(e);
		DispatcherUnhandledException += (_, args) =>
		{
			AppLogger.Write(args.Exception, "UI dispatcher exception");
			MessageBox.Show(
				Strings.Format("Errors.AppDetails", args.Exception, AppLogger.LogDirectoryPath),
				Strings.Get("Errors.AppTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			args.Handled = true;
		};
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception exception)
			{
				AppLogger.Write(exception, "AppDomain unhandled exception");
			}
		};
		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			AppLogger.Write(args.Exception, "Unobserved task exception");
			args.SetObserved();
		};

		MainWindow = new MainWindow();
		MainWindow.Show();
	}
}