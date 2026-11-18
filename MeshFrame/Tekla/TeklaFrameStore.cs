// Gắn mã khung vào part chính (UDA) và tìm / xóa khung đã tạo - phục vụ "Sửa khung đã chọn".
// Thông số + điểm pick lưu trong file cạnh model: <thư mục model>\MeshFrame\<mã>.mfr
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MeshFrame.Tekla
{
    public static class TeklaFrameStore
    {
        public const string IdKey = "MESHFRAME_ID";

        /// <summary>Thư mục của model đang mở ("" nếu model chưa lưu).</summary>
        public static string ModelFolder(TeklaApi api, object model)
        {
            object info = api.Call(model, "GetInfo");
            return Convert.ToString(api.Get(info, "ModelPath")) ?? "";
        }

        public static string RecordPath(string modelFolder, string id) { return Path.Combine(modelFolder, "MeshFrame", id + ".mfr"); }

        public static void WriteId(TeklaApi api, object mainPart, string id) { api.Call(mainPart, "SetUserProperty", IdKey, id); }

        public static string ReadId(TeklaApi api, object obj)
        {
            try
            {
                object[] r = api.CallRef(obj, "GetUserProperty", IdKey, "");
                return Convert.ToString(r[1]) ?? "";
            }
            catch (MissingMethodException) { return ""; }
        }

        /// <summary>Tìm khung từ đối tượng đang chọn: đối tượng hoặc part chính của assembly của nó có mã khung. Trả về mã (hoặc "") và part chính.</summary>
        public static string FindSelected(TeklaApi api, out object mainPart)
        {
            mainPart = null;
            object en = api.Call(api.New(api.ModelType("UI.ModelObjectSelector")), "GetSelectedObjects");
            while ((bool)api.Call(en, "MoveNext"))
            {
                object o = api.Get(en, "Current");
                if (o == null) continue;
                string id = ReadId(api, o);
                object main = o;
                if (id.Length == 0)
                {
                    try
                    {
                        object asm = api.Call(o, "GetAssembly");
                        if (asm == null) continue;                          // part không thuộc assembly nào (vd khung tạo từ bản cũ bị dở dang)
                        main = api.Call(asm, "GetMainPart");
                        if (main == null) continue;
                        id = ReadId(api, main);
                    }
                    catch (MissingMethodException) { continue; }
                }
                if (id.Length > 0) { mainPart = main; return id; }
            }
            return "";
        }

        /// <summary>Xóa mọi part của assembly chứa part chính (part phụ trước, part chính sau). Trả về số part đã xóa.</summary>
        public static int DeleteFrame(TeklaApi api, object mainPart)
        {
            object asm = api.Call(mainPart, "GetAssembly");
            var parts = new List<object>();
            IEnumerable secs = api.Call(asm, "GetSecondaries") as IEnumerable;
            if (secs != null) foreach (object o in secs) parts.Add(o);
            parts.Add(mainPart);
            int n = 0;
            foreach (object p in parts)
            {
                api.Call(p, "Delete");
                n++;
            }
            return n;
        }
    }
}
