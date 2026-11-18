using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MeshFrame.App;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += (object s, UnhandledExceptionEventArgs e) =>
		{
			Fail(e.ExceptionObject as Exception);
		};
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (object s, ThreadExceptionEventArgs e) =>
		{
			Fail(e.Exception);
		};
		try
		{
			return Start(args);
		}
		catch (Exception ex)
		{
			Fail(ex);
			return 1;
		}
	}

	private static void Fail(Exception ex)
	{
		string text = ((ex == null) ? "Lỗi không rõ." : ex.ToString());
		string text2 = null;
		string[] array = new string[3]
		{
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame"),
			AppDomain.CurrentDomain.BaseDirectory,
			Path.GetTempPath()
		};
		foreach (string text3 in array)
		{
			try
			{
				Directory.CreateDirectory(text3);
				text2 = Path.Combine(text3, "MeshFrame-error.log");
				File.AppendAllText(text2, DateTime.Now.ToString("s") + "\r\n" + text + "\r\n\r\n");
			}
			catch (Exception)
			{
				text2 = null;
				continue;
			}
			break;
		}
		try
		{
			MessageBox.Show("MeshFrame gặp lỗi:\n\n" + ((ex == null) ? text : ex.Message) + ((text2 == null) ? "" : ("\n\nChi tiết: " + text2)), "MeshFrame", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		catch (Exception)
		{
		}
	}

	private static int Start(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		if (!TeklaLocator.Resolve(args, out var release, out var bin))
		{
			MessageBox.Show("Không tìm thấy Tekla.Structures.dll + Tekla.Structures.Model.dll.\n\n• chạy với  --tekla-version=21.1 | 2020 | 2025\n• hoặc  --tekla-bin=\"C:\\TeklaStructures\\2025.0\\bin\"\n• hoặc ghi  TeklaBin=...  vào MeshFrame.ini cạnh file exe.", "MeshFrame");
			return 2;
		}
		return Runner.Run(release, bin);
	}
}
