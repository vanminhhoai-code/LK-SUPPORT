using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using MeshFrame.Core;
using MeshFrame.Tekla;
using MeshFrame.UI;

namespace MeshFrame.App;

internal static class Runner
{
	private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "settings.ini");

	public static int Run(string release, string bin)
	{
		string title = "MeshFrame - Tekla " + release;
		TeklaApi api;
		object model;
		try
		{
			api = TeklaApi.Load(bin);
			model = api.New(api.ModelType("Model"));
			if (!(bool)api.Call(model, "GetConnectionStatus"))
			{
				MessageBox.Show("Chưa kết nối được Tekla " + release + ". Mở Tekla và một model trước, rồi chạy lại MeshFrame.", title);
				return 3;
			}
		}
		catch (Exception e)
		{
			MessageBox.Show("Không nạp được API Tekla " + release + " (" + bin + "):\n" + Root(e).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return 3;
		}
		Func<string> apiCheck = () =>
		{
			TeklaContract.Report report = TeklaContract.VerifyLoaded(api);
			string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeshFrame", "api-missing.txt");
			if (report.Required.Count == 0 && report.Optional.Count == 0)
			{
				return "Tekla " + release + ": đủ mọi hàm API mà MeshFrame dùng.";
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			List<string> list2 = new List<string>();
			list2.AddRange(report.Required.ConvertAll((string x) => "BẮT BUỘC: " + x));
			list2.AddRange(report.Optional.ConvertAll((string x) => "tùy chọn (đã có dự phòng): " + x));
			File.WriteAllLines(text2, list2.ToArray());
			List<string> list3 = new List<string>();
			for (int num = 0; num < Math.Min(8, report.Required.Count); num++)
			{
				list3.Add("• " + report.Required[num].Split(new string[1] { "   <--" }, StringSplitOptions.None)[0]);
			}
			string text3 = ((report.Required.Count > 0) ? ("Tekla " + release + " THIẾU " + report.Required.Count + " hàm API BẮT BUỘC của MeshFrame:\n\n" + string.Join("\n", list3.ToArray()) + ((report.Required.Count > 8) ? "\n..." : "") + "\n\nChi tiết: " + text2 + "\nGửi file này cho người hỗ trợ.") : ("Tekla " + release + ": đủ mọi hàm bắt buộc. Có " + report.Optional.Count + " hàm tùy chọn không có trên bản này; app đã có dự phòng nên không ảnh hưởng (chi tiết: " + text2 + ")."));
			return (report.Required.Count <= 0) ? text3 : ("THIẾU|" + text3);
		};
		try
		{
			string text = apiCheck();
			if (text.StartsWith("THIẾU|"))
			{
				MessageBox.Show(text.Substring(6), title, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
		catch (Exception)
		{
		}
		TeklaFrameService svc = new TeklaFrameService(api, model);
		PickResult lastPick = null;
		Func<V3[]> func = () =>
		{
			if (lastPick != null && lastPick.Cleanup != null)
			{
				lastPick.Cleanup();
			}
			try
			{
				lastPick = TeklaPicker.PickPolygon(api, model);
			}
			catch (Exception e2)
			{
				MessageBox.Show("Lỗi khi pick (bản Tekla này thiếu hàm API?):\n" + Root(e2).GetType().Name + ": " + Root(e2).Message, title, MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return (V3[])null;
			}
			if (!lastPick.Complete)
			{
				MessageBox.Show("Pick dừng ở " + lastPick.Picked + " điểm, cần ít nhất 3 điểm. Chưa có gì được tạo.\n" + lastPick.StopReason, title, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return (V3[])null;
			}
			if (lastPick.DrawNote != null)
			{
				MessageBox.Show(lastPick.DrawNote, title, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			return lastPick.Points;
		};
		using (MeshFrameDialog meshFrameDialog = new MeshFrameDialog())
		{
			meshFrameDialog.Text = "MESHFRAME 1.2.8 - Tekla " + release;
			string modelName = "";
			try
			{
				modelName = Path.GetFileName(TeklaFrameStore.ModelFolder(api, model).TrimEnd('\\', '/'));
			}
			catch (Exception)
			{
			}
			meshFrameDialog.SetConnection(release, "API đã nạp từ Tekla", modelName, connected: true);
			meshFrameDialog.Settings = SettingsStore.Load(SettingsPath);
			meshFrameDialog.PickHandler = () => { var pts=func(); if(pts!=null)svc.StopEditing(); return pts; };
			meshFrameDialog.ViewProvider = () => TeklaViewReader.Read(api,model);
            meshFrameDialog.SelectedObjectProbe = () => TeklaProbe.DumpSelected(api);
			meshFrameDialog.ApiCheckHandler = () =>
			{
				string text2 = apiCheck();
				return (!text2.StartsWith("THIẾU|")) ? text2 : text2.Substring(6);
			};
			meshFrameDialog.CreateHandler = (MeshFrameSettings s, V3[] pts) =>
			{
				if(meshFrameDialog.EditingId==null)svc.StopEditing();
				else if(meshFrameDialog.EditingId!=svc.EditingId)return new CreateOutcome{Success=false,Message="Trạng thái khung đang sửa không khớp. Chọn lại khung hoặc Pick điểm để tạo mới."};
				ServiceResult serviceResult = svc.Create(s, pts);
				if(serviceResult.Success)serviceResult.Message += Environment.NewLine+svc.ShowCreated(serviceResult,serviceResult.Corners??pts);
				return new CreateOutcome
				{
					Success = serviceResult.Success,
					Points = serviceResult.Corners,
					Message = serviceResult.Message
				};
			};
			meshFrameDialog.EditHandler = () =>
			{
				try
				{
					FrameRecord frameRecord = svc.LoadSelected(out var error);
					return (frameRecord == null) ? new LoadedFrame
					{
						Error = error
					} : new LoadedFrame
					{
						Settings = frameRecord.Settings,
						Points = frameRecord.Corners,
						Id = frameRecord.Id
					};
				}
				catch (Exception e2)
				{
					return new LoadedFrame
					{
						Error = "Không đọc được khung đang chọn: " + Root(e2).GetType().Name + ": " + Root(e2).Message + "\n(Khung tạo bằng bản cũ không có mã khung - hãy tạo lại bằng bản này.)"
					};
				}
			};
			try
			{
				if (api.HasCatalogs)
				{
					List<string[]> list = new List<string[]>();
					foreach (RebarEntry item in TeklaCatalog.ReadRebars(api))
					{
						list.Add(new string[2] { item.Grade, item.Size });
					}
					meshFrameDialog.SetRebarCatalog(list);
				}
			}
			catch (Exception)
			{
			}
			// Picking is an explicit user action; startup opens the editor immediately.
			meshFrameDialog.ShowDialog();
			try
			{
				SettingsStore.Save(meshFrameDialog.Settings, SettingsPath);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
		if (lastPick != null && lastPick.Cleanup != null)
		{
			lastPick.Cleanup();
		}
		return 0;
	}

	private static Exception Root(Exception e)
	{
		while (e.InnerException != null)
		{
			e = e.InnerException;
		}
		return e;
	}
}
