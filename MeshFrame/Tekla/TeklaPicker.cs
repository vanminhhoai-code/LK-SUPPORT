// Pick đa giác (N điểm) trong Tekla qua TeklaApi (reflection): sau MỖI điểm vẽ thật trong Tekla (đồ họa tạm) kèm số đo.
// Kết thúc bằng chuột giữa (Tekla ngắt lệnh pick): từ 3 điểm trở lên coi là hoàn tất, app tự nối điểm cuối về điểm đầu.
using System;
using System.Collections.Generic;
using System.Globalization;
using MeshFrame.Core;

namespace MeshFrame.Tekla
{
    public sealed class PickResult
    {
        public V3[] Points;                 // các điểm (toạ độ GLOBAL); null nếu chưa đủ 3 điểm
        public int Picked;                  // số điểm đã pick
        public string StopReason;           // vì sao dừng (chuột giữa / Esc / lỗi) - tên loại lỗi + nội dung
        public string DrawNote;             // ghi chú nếu không vẽ được đồ họa (vd Tekla bản cũ không có GraphicsDrawer)
        public Action Cleanup;              // xóa đồ họa tạm đã vẽ (gọi khi pick lại / thoát)
        public bool Complete { get { return Points != null; } }
    }

    public static class TeklaPicker
    {
        /// <summary>Lời nhắc trên thanh trạng thái Tekla: CHỈ ASCII (Tekla không hiển thị được tiếng Việt có dấu ở đây).</summary>
        public static string Prompt(int index, double lastLen, double perimeter)
        {
            string tail = index > 2
                ? " | doan truoc " + lastLen.ToString("0.0", CultureInfo.InvariantCulture) + " mm | chu vi " + perimeter.ToString("0.0", CultureInfo.InvariantCulture) + " mm"
                : "";
            return "MeshFrame: pick diem P" + index + tail + " | chuot giua = ket thuc (>= 3 diem)";
        }

        public static PickResult PickPolygon(TeklaApi api, object model)
        {
            object picker = api.New(api.ModelType("UI.Picker"));
            object plane = api.Call(api.Call(model, "GetWorkPlaneHandler"), "GetCurrentTransformationPlane");
            object toGlobal = api.Get(plane, "TransformationMatrixToGlobal");

            var res = new PickResult();
            object drawer = null;
            try { drawer = api.New(api.ModelType("UI.GraphicsDrawer")); }
            catch (Exception) { res.DrawNote = "Tekla bản này không có GraphicsDrawer: số đo chỉ hiện trên thanh trạng thái."; }
            var ids = new List<object>();
            res.Cleanup = delegate { Remove(api, drawer, ids); ids.Clear(); };

            var pts = new List<V3>();
            var local = new List<object>();
            double perimeter = 0, last = 0;
            bool useRef = true;                                                 // PickPoint(chuỗi, điểm trước): Tekla 21.1 KHÔNG có hàm này
            while (true)
            {
                int k = pts.Count;
                object p;
                try
                {
                    string prompt = Prompt(k + 1, last, perimeter);
                    p = PickOne(api, picker, prompt, k > 0 ? local[k - 1] : null, ref useRef);
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    if (IsApiError(ex)) throw;                                  // thiếu hàm của bản Tekla này: báo lỗi thật, KHÔNG coi là dừng pick
                    res.StopReason = ex.GetType().Name + ": " + ex.Message;     // chuột giữa / Esc / Tekla dừng pick
                    break;
                }
                object g = api.Call(toGlobal, "Transform", p);                  // lỗi đổi toạ độ được ném ra, không nuốt
                var gp = new V3(api.GetDouble(g, "X"), api.GetDouble(g, "Y"), api.GetDouble(g, "Z"));
                if (k > 0) { last = (gp - pts[k - 1]).Length; perimeter += last; }
                pts.Add(gp); local.Add(p);
                res.Picked = pts.Count;
                if (drawer != null)
                {
                    try { Draw(api, drawer, ids, local, k, last); }
                    catch (Exception ex) { drawer = null; res.DrawNote = "Không vẽ được đồ họa tạm (" + ex.GetType().Name + "): số đo chỉ hiện trên thanh trạng thái."; }
                }
            }
            if (pts.Count >= 3) res.Points = pts.ToArray();
            return res;
        }

        /// <summary>Lỗi do bản Tekla thiếu hàm/kiểu (khác với người dùng ngắt pick).</summary>
        public static bool IsApiError(Exception ex)
        {
            return ex is MissingMemberException || ex is TypeLoadException || ex is MethodAccessException || ex is InvalidCastException;
        }

        /// <summary>Pick một điểm: ưu tiên PickPoint(chuỗi, điểm trước) để Tekla vẽ dây thun; bản Tekla không có hàm đó thì dùng PickPoint(chuỗi).</summary>
        private static object PickOne(TeklaApi api, object picker, string prompt, object previous, ref bool useRef)
        {
            if (previous != null && useRef)
            {
                try { return api.Call(picker, "PickPoint", prompt, previous); }
                catch (MissingMethodException) { useRef = false; }
            }
            return api.Call(picker, "PickPoint", prompt);
        }

        // Điểm k: dấu thập + nhãn "P{k+1}"; từ điểm thứ 2: đoạn nối từ điểm trước + chiều dài ở giữa đoạn.
        private static void Draw(TeklaApi api, object drawer, List<object> ids, List<object> local, int k, double len)
        {
            object red = api.New(api.ModelType("UI.Color"), 1.0, 0.0, 0.0);
            object blue = api.New(api.ModelType("UI.Color"), 0.0, 0.3, 1.0);
            double x = api.GetDouble(local[k], "X"), y = api.GetDouble(local[k], "Y"), z = api.GetDouble(local[k], "Z");
            const double m = 60;
            ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(x - m, y, z), api.Point(x + m, y, z), red));
            ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(x, y - m, z), api.Point(x, y + m, z), red));
            ids.Add(api.Call(drawer, "DrawText", api.Point(x + m, y + m, z), "P" + (k + 1), red));
            if (k > 0)
            {
                double px = api.GetDouble(local[k - 1], "X"), py = api.GetDouble(local[k - 1], "Y"), pz = api.GetDouble(local[k - 1], "Z");
                ids.Add(api.Call(drawer, "DrawLineSegment", api.Point(px, py, pz), api.Point(x, y, z), blue));
                ids.Add(api.Call(drawer, "DrawText", api.Point((px + x) / 2, (py + y) / 2, (pz + z) / 2), len.ToString("0.0", CultureInfo.InvariantCulture) + " mm", blue));
            }
        }

        private static void Remove(TeklaApi api, object drawer, List<object> ids)
        {
            if (drawer == null || ids.Count == 0) return;
            try
            {
                var list = new List<int>();
                foreach (object o in ids) if (o is int) list.Add((int)o);
                api.Call(drawer, "RemoveTemporaryGraphicsObjects", list);
            }
            catch (Exception) { }
        }
    }
}
