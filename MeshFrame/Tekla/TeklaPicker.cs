// Pick 4 điểm trong Tekla qua TeklaApi (reflection). Tách riêng để test được trên SDK giả và đối chiếu hợp đồng với DLL thật.
using System;
using MeshFrame.Core;

namespace MeshFrame.Tekla
{
    public sealed class PickResult
    {
        public V3[] Corners;           // đủ 4 điểm (toạ độ GLOBAL) khi thành công
        public int Picked;             // số điểm đã pick xong
        public string StopReason;      // lý do dừng giữa chừng (Esc / lỗi của Tekla)
        public bool Complete { get { return Corners != null; } }
    }

    public static class TeklaPicker
    {
        /// <summary>Pick đủ 4 điểm. Dừng giữa chừng (Esc hay Tekla ném lỗi lúc pick) -> Corners = null kèm StopReason.
        /// Lỗi khi đổi toạ độ (không phải do người dùng) được ném ra, không bị nuốt.</summary>
        public static PickResult PickCorners(TeklaApi api, object model)
        {
            object picker = api.New(api.ModelType("UI.Picker"));
            object plane = api.Call(api.Call(model, "GetWorkPlaneHandler"), "GetCurrentTransformationPlane");
            object toGlobal = api.Get(plane, "TransformationMatrixToGlobal");

            var res = new PickResult();
            var c = new V3[4];
            for (int i = 0; i < 4; i++)
            {
                object p;
                try
                {
                    p = api.Call(picker, "PickPoint", "MeshFrame: pick điểm P" + (i + 1) + " / 4 (đi liên tiếp quanh khung, ngược chiều kim đồng hồ nhìn từ trên)");
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    res.StopReason = ex.GetType().Name + ": " + ex.Message;
                    return res;
                }
                object g = api.Call(toGlobal, "Transform", p);
                c[i] = new V3(api.GetDouble(g, "X"), api.GetDouble(g, "Y"), api.GetDouble(g, "Z"));
                res.Picked = i + 1;
            }
            res.Corners = c;
            return res;
        }
    }
}
